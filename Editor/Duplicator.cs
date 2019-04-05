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
        /// <param name="duplicatingOptionals">モデル情報 (VRMMeta) とテクスチャも複製するなら <c>true</c>。</param>
        public static void Duplicate(GameObject sourceAvatar, string destinationPath, bool duplicatingOptionals = false)
        {
            Duplicator.DuplicatePrefab(sourceAvatar: sourceAvatar, destinationPath: destinationPath, duplicatingOptionals: duplicatingOptionals);
            Duplicator.DuplicateMeshes(prefabPath: destinationPath);
            Duplicator.DuplicateMaterials(prefabPath: destinationPath);
            if (duplicatingOptionals)
            {
                Duplicator.DuplicateTextures(prefabPath: destinationPath);
            }
            Duplicator.DuplicateVRMBlendShapes(prefabPath: destinationPath);
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
            var destinationFolderUnityPath = UnityPath.FromUnityPath(prefabPath);
            Type type = typeof(T);
            foreach (KeyValuePair<Type, string> typeAndSuffix in Duplicator.FolderNameSuffixes)
            {
                if (typeAndSuffix.Key.IsAssignableFrom(type))
                {
                    destinationFolderUnityPath = destinationFolderUnityPath.GetAssetFolder(suffix: typeAndSuffix.Value);
                    break;
                }
            }

            destinationFolderUnityPath.EnsureFolder();

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

            string destinationPath = destinationFolderUnityPath.Child(destinationFileName).Value;
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
        private static void DuplicatePrefab(GameObject sourceAvatar, string destinationPath, bool duplicatingOptionals)
        {
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
        }

        /// <summary>
        /// プレハブが依存しているメッシュを複製します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        private static void DuplicateMeshes(string prefabPath)
        {
            var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;

            var alreadyDuplicatedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = renderer.sharedMesh;
                renderer.sharedMesh = alreadyDuplicatedMeshes.ContainsKey(mesh)
                    ? alreadyDuplicatedMeshes[mesh]
                    : Duplicator.DuplicateAssetToFolder<Mesh>(source: mesh, prefabPath: prefabPath);
                alreadyDuplicatedMeshes[mesh] = renderer.sharedMesh;
            }

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                filter.sharedMesh = alreadyDuplicatedMeshes.ContainsKey(mesh)
                    ? alreadyDuplicatedMeshes[mesh]
                    : Duplicator.DuplicateAssetToFolder<Mesh>(source: mesh, prefabPath: prefabPath);
                alreadyDuplicatedMeshes[mesh] = filter.sharedMesh;
            }
        }

        /// <summary>
        /// プレハブが依存しているマテリアルを複製します。
        /// </summary>
        /// <param name="prefabPath">「Assets/」から始まるプレハブのパス。</param>
        private static void DuplicateMaterials(string prefabPath)
        {
            var alreadyDuplicatedMaterials = new Dictionary<Material, Material>();

            foreach (var renderer in (AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject).GetComponentsInChildren<Renderer>())
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
        private static void DuplicateTextures(string prefabPath)
        {
            var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;

            var alreadyDuplicatedTextures = new Dictionary<Texture, Texture>();

            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
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

            VRMMetaObject meta = prefab.GetComponent<VRMMeta>().Meta;
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
        private static void DuplicateVRMBlendShapes(string prefabPath)
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
