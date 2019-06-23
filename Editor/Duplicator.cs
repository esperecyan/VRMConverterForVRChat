using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using UniHumanoid;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// アバターの複製を行います。
    /// </summary>
    public class Duplicator
    {
        /// <summary>
        /// アセットの種類ごとの、複製先のフォルダ名の末尾に追加する文字列。
        /// </summary>
        private static Dictionary<Type, string> FolderNameSuffixes = new Dictionary<Type, string> {
            { typeof(Mesh              ), ".Meshes"      },
            { typeof(Material          ), ".Materials"   },
            { typeof(Texture           ), ".Textures"    },
            { typeof(BlendShapeAvatar  ), ".BlendShapes" },
            { typeof(BlendShapeClip    ), ".BlendShapes" },
            { typeof(UnityEngine.Object), ".VRChat"      },
        };

        /// <summary>
        /// オブジェクトを複製します。
        /// </summary>
        /// <param name="sourceAvatar">プレハブ、またはHierarchy上のオブジェクト。</param>
        /// <param name="destinationPath">「Assets/」から始まり「.prefab」で終わる複製先のパス。</param>
        /// <param name="duplicatingOptionals">モデル情報 (VRMMeta) とVRMのBlendShapeとテクスチャも複製するなら <c>true</c>。</param>
        /// <param name="notCombineRendererObjectNames">結合しないメッシュレンダラーのオブジェクト名。</param>
        /// <returns>複製後のインスタンス。</returns>
        public static GameObject Duplicate(
            GameObject sourceAvatar,
            string destinationPath,
            bool duplicatingOptionals = false,
            IEnumerable<string> notCombineRendererObjectNames = null
        )
        {
            GameObject destinationPrefab = Duplicator.DuplicatePrefab(
                sourceAvatar: sourceAvatar,
                destinationPath: destinationPath,
                duplicatingOptionals: duplicatingOptionals
            );
            var destinationPrefabInstance = PrefabUtility
                .InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath)) as GameObject;

            Duplicator.DuplicateAndCombineMeshes(
                prefabPath: destinationPath,
                prefabInstance: destinationPrefabInstance,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );
            Duplicator.DuplicateMaterials(prefabPath: destinationPath, prefabInstance: destinationPrefabInstance);
            if (duplicatingOptionals)
            {
                Duplicator.DuplicateTextures(prefabPath: destinationPath, prefabInstance: destinationPrefabInstance);
                Duplicator
                    .DuplicateVRMBlendShapes(prefabPath: destinationPath, prefabInstance: destinationPrefabInstance);
            }

            PrefabUtility
                .ReplacePrefab(destinationPrefabInstance, destinationPrefab, ReplacePrefabOptions.ConnectToPrefab);
            destinationPrefabInstance.transform.SetAsLastSibling();
            return destinationPrefabInstance;
        }

        /// <summary>
        /// アセットの種類に応じて、保存先を決定します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="type">アセットの種類。</param>
        /// <param name="fileName">ファイル名。</param>
        /// <returns>「Assets/」から始まるパス。</returns>
        internal static string DetermineAssetPath(string prefabPath, Type type, string fileName = "")
        {
            var destinationFolderUnityPath = UnityPath.FromUnityPath(prefabPath);
            foreach (KeyValuePair<Type, string> typeAndSuffix in Duplicator.FolderNameSuffixes)
            {
                if (typeAndSuffix.Key.IsAssignableFrom(type))
                {
                    destinationFolderUnityPath = destinationFolderUnityPath.GetAssetFolder(suffix: typeAndSuffix.Value);
                    break;
                }
            }

            destinationFolderUnityPath.EnsureFolder();

            return destinationFolderUnityPath.Child(fileName).Value;
        }

        /// <summary>
        /// アセットをプレハブが置かれているディレクトリの直下のフォルダへ複製します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">複製元のオブジェクト。</param>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="fileName">ファイル名が複製元と異なる場合に指定。</param>
        /// <returns></returns>
        internal static T DuplicateAssetToFolder<T>(UnityEngine.Object source, string prefabPath, string fileName = "") where T : UnityEngine.Object
        {
            string destinationFileName;
            if (string.IsNullOrEmpty(fileName))
            {
                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrEmpty(sourcePath) || AssetDatabase.IsSubAsset(source))
                {
                    destinationFileName = source.name.EscapeFilePath() + ".asset";
                }
                else
                {
                    destinationFileName = Path.GetFileName(sourcePath);
                }
            }
            else
            {
                destinationFileName = fileName;
            }

            string destinationPath
                = Duplicator.DetermineAssetPath(prefabPath: prefabPath, type: typeof(T), fileName: destinationFileName);
            Duplicator.DuplicateAsset(source: source, destinationPath: destinationPath);

            return AssetDatabase.LoadAssetAtPath<T>(destinationPath);
        }

        /// <summary>
        /// アセットをプレハブが置かれているディレクトリの直下のフォルダへ複製します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">複製元のオブジェクト。</param>
        /// <param name="prefabInstance">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="fileName">ファイル名が複製元と異なる場合に指定。</param>
        /// <returns></returns>
        internal static T DuplicateAssetToFolder<T>(UnityEngine.Object source, GameObject prefabInstance, string fileName = "") where T : UnityEngine.Object
        {
            return DuplicateAssetToFolder<T>(
                source: source,
                prefabPath: AssetDatabase.GetAssetPath(PrefabUtility.GetPrefabParent(prefabInstance)),
                fileName: fileName
            );
        }

        /// <summary>
        /// アセットインスタンスを複製します。
        /// </summary>
        /// <remarks>
        /// オブジェクト名の末尾に「(Clone)」が付加されないようにします。
        /// </remarks>
        /// <param name="instance"></param>
        /// <returns></returns>
        private static UnityEngine.Object DuplicateAssetInstance(UnityEngine.Object instance)
        {
            UnityEngine.Object newInstance = UnityEngine.Object.Instantiate(instance);
            newInstance.name = instance.name;
            return newInstance;
        }

        /// <summary>
        /// アセットを複製します。
        /// </summary>
        /// <remarks>
        /// 複製先にすでにアセットが存在していれば上書きし、metaファイルは複製しません。
        /// </remarks>
        /// <param name="source"></param>
        /// <param name="duplicatedPath">「Assets/」から始まりファイル名で終わる複製先のパス。</param>
        private static void DuplicateAsset(UnityEngine.Object source, string destinationPath)
        {
            UnityEngine.Object destination = AssetDatabase.LoadMainAssetAtPath(destinationPath);
            if (destination)
            {
                if (AssetDatabase.IsNativeAsset(source) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source)))
                {
                    EditorUtility.CopySerialized(source, destination);
                }
                else
                {
                    string sourceFullPath = UnityPath.FromAsset(source).FullPath;
                    string destinationFullPath = destinationPath.AssetPathToFullPath();
                    if (File.GetLastWriteTime(sourceFullPath) != File.GetLastWriteTime(destinationFullPath))
                    {
                        File.Copy(
                            sourceFileName: sourceFullPath,
                            destFileName: destinationFullPath,
                            overwrite: true
                        );
                        AssetDatabase.ImportAsset(destinationPath);
                    }
                }
            }
            else
            {
                if (AssetDatabase.IsSubAsset(source) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source)))
                {
                    AssetDatabase.CreateAsset(Duplicator.DuplicateAssetInstance(source), destinationPath);
                }
                else
                {
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destinationPath);
                }
            }
        }

        /// <summary>
        /// ルートとなるプレハブを複製します。
        /// </summary>
        /// <param name="sourceAvatar">プレハブ、またはHierarchy上のオブジェクト。</param>
        /// <param name="destinationPath">「Assets/」から始まり「.prefab」で終わる複製先のパス。</param>
        /// <param name="duplicatingOptionals">モデル情報 (VRMMeta) も複製するなら <c>true</c>。</param>
        /// <returns></returns>
        private static GameObject DuplicatePrefab(
            GameObject sourceAvatar,
            string destinationPath,
            bool duplicatingOptionals
        ) {
            // プレハブ
            GameObject sourceInstance = UnityEngine.Object.Instantiate(sourceAvatar);
            PrefabUtility.DisconnectPrefabInstance(sourceInstance);
            var destinationPrefab = AssetDatabase.LoadMainAssetAtPath(destinationPath) as GameObject;
            if (destinationPrefab)
            {
                destinationPrefab = PrefabUtility.ReplacePrefab(sourceAvatar, destinationPrefab, ReplacePrefabOptions.ConnectToPrefab);
            }
            else
            {
                destinationPrefab = PrefabUtility.CreatePrefab(destinationPath, sourceInstance, ReplacePrefabOptions.ConnectToPrefab);
            }
            UnityEngine.Object.DestroyImmediate(sourceInstance);

            // Avatar
            var animator = destinationPrefab.GetComponent<Animator>();
            var destinationAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(destinationPath);
            if (destinationAvatar)
            {
                EditorUtility.CopySerialized(animator.avatar, destinationAvatar);
                animator.avatar = destinationAvatar;
            }
            else
            {
                animator.avatar = Duplicator.DuplicateAssetInstance(animator.avatar) as Avatar;
                AssetDatabase.AddObjectToAsset(animator.avatar, destinationPrefab);
            }

            // AvatarDescription
            var humanoidDescription = destinationPrefab.GetComponent<VRMHumanoidDescription>();
            humanoidDescription.Avatar = animator.avatar;
            var destinationAvatarDescription = AssetDatabase.LoadAssetAtPath<AvatarDescription>(destinationPath);
            if (destinationAvatarDescription)
            {
                EditorUtility.CopySerialized(humanoidDescription.Description, destinationAvatarDescription);
                humanoidDescription.Description = destinationAvatarDescription;
            }
            else
            {
                humanoidDescription.Description = Duplicator.DuplicateAssetInstance(humanoidDescription.Description) as AvatarDescription;
                AssetDatabase.AddObjectToAsset(humanoidDescription.Description, destinationPrefab);
            }

            if (duplicatingOptionals)
            {
                // Meta
                var meta = destinationPrefab.GetComponent<VRMMeta>();
                var vrmMetaObject = AssetDatabase.LoadAssetAtPath<VRMMetaObject>(destinationPath);
                if (vrmMetaObject)
                {
                    EditorUtility.CopySerialized(humanoidDescription.Description, vrmMetaObject);
                    meta.Meta = vrmMetaObject;
                }
                else
                {
                    meta.Meta = Duplicator.DuplicateAssetInstance(meta.Meta) as VRMMetaObject;
                    AssetDatabase.AddObjectToAsset(meta.Meta, destinationPrefab);
                }
            }

            return destinationPrefab;
        }

        /// <summary>
        /// プレハブが依存しているメッシュを複製・結合します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="prefabInstance"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        private static void DuplicateAndCombineMeshes(
            string prefabPath,
            GameObject prefabInstance,
            IEnumerable<string> notCombineRendererObjectNames
        ) {
            Duplicator.MakeAllVerticesHaveWeights(
                prefabInstance: prefabInstance,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );

            Duplicator.CombineAllMeshes(
                prefabPath: prefabPath,
                prefabInstance: prefabInstance,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );

            var alreadyDuplicatedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.name == VRChatUtility.AutoBlinkMeshPath)
                {
                    continue;
                }

                Mesh mesh = renderer.sharedMesh;
                renderer.sharedMesh = alreadyDuplicatedMeshes.ContainsKey(mesh)
                    ? alreadyDuplicatedMeshes[mesh]
                    : Duplicator.DuplicateAssetToFolder<Mesh>(
                        source: mesh,
                        prefabPath: prefabPath,
                        fileName: Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh))
                            == VRChatUtility.AutoBlinkMeshPath + ".asset"
                            ? VRChatUtility.AutoBlinkMeshPath + "-" + VRChatUtility.AutoBlinkMeshPath + ".asset"
                            : ""
                    );
                alreadyDuplicatedMeshes[mesh] = renderer.sharedMesh;
            }

            foreach (var filter in prefabInstance.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                filter.sharedMesh = alreadyDuplicatedMeshes.ContainsKey(mesh)
                    ? alreadyDuplicatedMeshes[mesh]
                    : Duplicator.DuplicateAssetToFolder<Mesh>(
                        source: mesh,
                        prefabPath: prefabPath,
                        fileName: Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh))
                            == VRChatUtility.AutoBlinkMeshPath + ".asset"
                            ? VRChatUtility.AutoBlinkMeshPath + "-" + VRChatUtility.AutoBlinkMeshPath + ".asset"
                            : ""
                    );
                alreadyDuplicatedMeshes[mesh] = filter.sharedMesh;
            }
        }

        /// <summary>
        /// すべてのメッシュの全頂点にウェイトが設定された状態にします。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        private static void MakeAllVerticesHaveWeights(
            GameObject prefabInstance,
            IEnumerable<string> notCombineRendererObjectNames
        ) {
            foreach (var renderer in prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (notCombineRendererObjectNames.Contains(renderer.name)
                    || renderer.bones.Length > 1 || renderer.bones[0] != renderer.transform)
                {
                    continue;
                }

                Transform bone = renderer.transform.parent;
                renderer.bones = new[] { bone };

                var mesh = Duplicator.DuplicateAssetInstance(instance: renderer.sharedMesh) as Mesh;
                mesh.bindposes = new[] { bone.worldToLocalMatrix * renderer.localToWorldMatrix };
                renderer.sharedMesh = mesh;
            }

            foreach (var meshRenderer in prefabInstance.GetComponentsInChildren<MeshRenderer>())
            {
                if (notCombineRendererObjectNames.Contains(meshRenderer.name))
                {
                    continue;
                }

                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                Material[] materials = meshRenderer.sharedMaterials;
                var renderer = meshRenderer.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = meshFilter.sharedMesh;
                UnityEngine.Object.DestroyImmediate(meshFilter);
                renderer.sharedMaterials = materials;

                Transform bone = renderer.transform.parent;
                renderer.bones = new[] { bone };

                var mesh = Duplicator.DuplicateAssetInstance(instance: renderer.sharedMesh) as Mesh;
                mesh.boneWeights = new BoneWeight[mesh.vertexCount].Select(boneWeight => {
                    boneWeight.weight0 = 1;
                    return boneWeight;
                }).ToArray();
                mesh.bindposes = new[] { bone.worldToLocalMatrix * renderer.localToWorldMatrix };
                renderer.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// メッシュ、サブメッシュを結合します。
        /// </summary>
        /// <param name="prefabPath"></param>
        /// <param name="prefabInstance"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        private static void CombineAllMeshes(
            string prefabPath,
            GameObject prefabInstance,
            IEnumerable<string> notCombineRendererObjectNames
        ) {
            SkinnedMeshRenderer bodyRenderer = MeshIntegrator
                .Integrate(go: prefabInstance, notCombineRendererObjectNames: notCombineRendererObjectNames);
            bodyRenderer.rootBone = prefabInstance.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);

            foreach (var renderer in prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer == bodyRenderer || notCombineRendererObjectNames.Contains(renderer.name))
                {
                    continue;
                }

                GameObject gameObject = renderer.gameObject;
                if (!gameObject)
                {
                    continue;
                }

                if (gameObject.transform.childCount > 0)
                {
                    UnityEngine.Object.DestroyImmediate(renderer);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            Transform sameNameTransform = prefabInstance.transform.Find(VRChatUtility.AutoBlinkMeshPath);
            if (sameNameTransform)
            {
                sameNameTransform.name += "-" + VRChatUtility.AutoBlinkMeshPath;
            }

            bodyRenderer.name = VRChatUtility.AutoBlinkMeshPath;
            bodyRenderer.sharedMesh.name = bodyRenderer.name;

            string destinationPath = Duplicator.DetermineAssetPath(
                prefabPath: prefabPath,
                type: typeof(Mesh),
                fileName: bodyRenderer.sharedMesh.name + ".asset"
            );
            var destination = AssetDatabase.LoadAssetAtPath<Mesh>(destinationPath);
            if (destination)
            {
                EditorUtility.CopySerialized(bodyRenderer.sharedMesh, destination);
                bodyRenderer.sharedMesh = destination;
            }
            else
            {
                AssetDatabase.CreateAsset(bodyRenderer.sharedMesh, destinationPath);
            }
        }

        /// <summary>
        /// プレハブが依存しているマテリアルを複製します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="prefabInstance"></param>
        private static void DuplicateMaterials(string prefabPath, GameObject prefabInstance)
        {
            var alreadyDuplicatedMaterials = new Dictionary<Material, Material>();

            foreach (var renderer in prefabInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material => {
                    if (alreadyDuplicatedMaterials.ContainsKey(material))
                    {
                        return alreadyDuplicatedMaterials[material];
                    }

                    return alreadyDuplicatedMaterials[material]
                        = Duplicator.DuplicateAssetToFolder<Material>(source: material, prefabPath: prefabPath);
                }).ToArray();
            }
        }

        /// <summary>
        /// プレハブが依存しているテクスチャを複製します。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// Get all textures from a material? — Unity Answers
        /// <https://answers.unity.com/answers/1116025/view.html>
        /// </remarks>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="prefabInstance"></param>
        private static void DuplicateTextures(string prefabPath, GameObject prefabInstance)
        {
            var alreadyDuplicatedTextures = new Dictionary<Texture, Texture>();

            foreach (var renderer in prefabInstance.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (!material)
                    {
                        continue;
                    }

                    Shader shader = material.shader;
                    for (var i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                        {
                            continue;
                        }

                        string propertyName = ShaderUtil.GetPropertyName(shader, i);
                        Texture texture = material.GetTexture(propertyName);
                        if (!texture || alreadyDuplicatedTextures.ContainsValue(texture))
                        {
                            continue;
                        }

                        Texture newTexture;
                        if (alreadyDuplicatedTextures.ContainsKey(texture))
                        {
                            newTexture = alreadyDuplicatedTextures[texture];
                        }
                        else
                        {
                            newTexture = Duplicator.DuplicateAssetToFolder<Texture>(source: texture, prefabPath: prefabPath);
                            alreadyDuplicatedTextures[texture] = newTexture;
                        }

                        material.SetTexture(propertyName, newTexture);
                    }
                }
            }

            VRMMetaObject meta = prefabInstance.GetComponent<VRMMeta>().Meta;
            Texture2D thumbnail = meta.Thumbnail;
            if (!thumbnail)
            {
                return;
            }
            meta.Thumbnail = (alreadyDuplicatedTextures.ContainsKey(thumbnail)
                ? alreadyDuplicatedTextures[thumbnail] as Texture2D
                : Duplicator.DuplicateAssetToFolder<Texture2D>(source: thumbnail, prefabPath: prefabPath));
            alreadyDuplicatedTextures[thumbnail] = meta.Thumbnail;
        }


        /// <summary>
        /// プレハブが依存しているVRMブレンドシェイプを複製します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        /// <param name="prefabInstance"></param>
        private static void DuplicateVRMBlendShapes(string prefabPath, GameObject prefabInstance)
        {
            var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;

            var proxy = prefab.GetComponent<VRMBlendShapeProxy>();
            if (!proxy || !proxy.BlendShapeAvatar)
            {
                return;
            }

            proxy.BlendShapeAvatar = Duplicator.DuplicateAssetToFolder<BlendShapeAvatar>(source: proxy.BlendShapeAvatar, prefabPath: prefabPath); 

            var alreadyDuplicatedBlendShapeClips = new Dictionary<BlendShapeClip, BlendShapeClip>();

            proxy.BlendShapeAvatar.Clips = proxy.BlendShapeAvatar.Clips.Select(clip => {
                if (!clip)
                {
                    return clip;
                }

                if (alreadyDuplicatedBlendShapeClips.ContainsKey(clip))
                {
                    return alreadyDuplicatedBlendShapeClips[clip];
                }

                return alreadyDuplicatedBlendShapeClips[clip]
                    = Duplicator.DuplicateAssetToFolder<BlendShapeClip>(source: clip, prefabPath: prefabPath);
            }).ToList();
        }
    }
}
