using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using UniHumanoid;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;

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
        /// <param name="notCombineRendererObjectNames">結合しないメッシュレンダラーのオブジェクト名。</param>
        /// <param name="combineMeshesAndSubMeshes">メッシュ・サブメッシュを結合するなら <c>true</c>。</param>
        /// <returns>複製後のインスタンス。</returns>
        public static GameObject Duplicate(
            GameObject sourceAvatar,
            string destinationPath,
            IEnumerable<string> notCombineRendererObjectNames = null,
            bool combineMeshesAndSubMeshes = true
        )
        {
            GameObject destinationPrefab = Duplicator.DuplicatePrefab(
                sourceAvatar: sourceAvatar,
                destinationPath: destinationPath
            );
            var destinationPrefabInstance = PrefabUtility.InstantiatePrefab(destinationPrefab) as GameObject;

            Duplicator.DuplicateAndCombineMeshes(
                prefabInstance: destinationPrefabInstance,
                combineMeshesAndSubMeshes: combineMeshesAndSubMeshes,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );
            Duplicator.DuplicateMaterials(prefabInstance: destinationPrefabInstance);

            PrefabUtility.RecordPrefabInstancePropertyModifications(destinationPrefabInstance);
            destinationPrefabInstance.transform.SetAsLastSibling();
            return destinationPrefabInstance;
        }

        /// <summary>
        /// アセットの種類に応じて、保存先を決定します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <param name="type">アセットの種類。</param>
        /// <param name="fileName">ファイル名。</param>
        /// <returns>「Assets/」から始まるパス。</returns>
        internal static string DetermineAssetPath(GameObject prefabInstance, Type type, string fileName = "")
        {
            var destinationFolderUnityPath
                = UnityPath.FromUnityPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance));
            foreach (var (assetType, suffix) in Duplicator.FolderNameSuffixes)
            {
                if (assetType.IsAssignableFrom(type))
                {
                    destinationFolderUnityPath = destinationFolderUnityPath.GetAssetFolder(suffix);
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
        /// <param name="prefabInstance">プレハブインスタンス。</param>
        /// <param name="fileName">ファイル名が複製元と異なる場合に指定。</param>
        /// <returns></returns>
        internal static T DuplicateAssetToFolder<T>(T source, GameObject prefabInstance, string fileName = "")
            where T : UnityEngine.Object
        {
            string destinationFileName;
            if (string.IsNullOrEmpty(fileName))
            {
                var sourceUnityPath = UnityPath.FromAsset(source);
                if (!sourceUnityPath.IsUnderAssetsFolder || AssetDatabase.IsSubAsset(source))
                {
                    destinationFileName = source.name.EscapeFilePath() + ".asset";
                }
                else
                {
                    destinationFileName = Path.GetFileName(sourceUnityPath.Value);
                }
            }
            else
            {
                destinationFileName = fileName;
            }

            string destinationPath = Duplicator
                .DetermineAssetPath(prefabInstance: prefabInstance, type: typeof(T), fileName: destinationFileName);
            Duplicator.DuplicateAsset(source: source, destinationPath: destinationPath);

            return AssetDatabase.LoadAssetAtPath<T>(destinationPath);
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
            var sourceUnityPath = UnityPath.FromAsset(source);
            UnityEngine.Object destination = AssetDatabase.LoadMainAssetAtPath(destinationPath);
            if (destination)
            {
                if (AssetDatabase.IsNativeAsset(source) || !sourceUnityPath.IsUnderAssetsFolder)
                {
                    EditorUtility.CopySerialized(source, destination);
                }
                else
                {
                    string sourceFullPath = sourceUnityPath.FullPath;
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
                if (AssetDatabase.IsSubAsset(source) || !sourceUnityPath.IsUnderAssetsFolder)
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
        /// <returns></returns>
        private static GameObject DuplicatePrefab(
            GameObject sourceAvatar,
            string destinationPath
        ) {
            // プレハブ
            GameObject sourceInstance = UnityEngine.Object.Instantiate(sourceAvatar);
            GameObject destinationPrefab = PrefabUtility
                .SaveAsPrefabAssetAndConnect(sourceInstance, destinationPath, InteractionMode.AutomatedAction);
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

            // AvatarDescription (最終的に削除するので、アセットは複製しない)
            var humanoidDescription = destinationPrefab.GetComponent<VRMHumanoidDescription>();
            humanoidDescription.Avatar = animator.avatar;
            humanoidDescription.Description
                = Duplicator.DuplicateAssetInstance(humanoidDescription.Description) as AvatarDescription;

            return destinationPrefab;
        }

        /// <summary>
        /// もっともシェイプキーが多いメッシュを取得します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static SkinnedMeshRenderer GetFaceMeshRenderer(GameObject prefabInstance)
        {
            return prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(renderer => renderer.sharedMesh ? renderer.sharedMesh.blendShapeCount : 0).First();
        }

        /// <summary>
        /// プレハブが依存しているメッシュを複製・結合します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <param name="combineMeshesAndSubMeshes"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        private static void DuplicateAndCombineMeshes(
            GameObject prefabInstance,
            bool combineMeshesAndSubMeshes,
            IEnumerable<string> notCombineRendererObjectNames
        ) {
            SkinnedMeshRenderer faceMeshRenderer
                = combineMeshesAndSubMeshes ? null : Duplicator.GetFaceMeshRenderer(prefabInstance: prefabInstance);

            Transform sameNameTransform = prefabInstance.transform.Find(VRChatUtility.AutoBlinkMeshPath);
            if (sameNameTransform && (combineMeshesAndSubMeshes || faceMeshRenderer.transform != sameNameTransform))
            {
                sameNameTransform.name += "-" + VRChatUtility.AutoBlinkMeshPath;
            }

            if (combineMeshesAndSubMeshes)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
                CombineMeshesAndSubMeshes.Combine(
                    root: prefabInstance,
                    notCombineRendererObjectNames: notCombineRendererObjectNames,
                    destinationObjectName: VRChatUtility.AutoBlinkMeshPath
                );
                PrefabUtility.SaveAsPrefabAssetAndConnect(prefabInstance, prefabPath, InteractionMode.AutomatedAction);
            }
            else
            {
                if (faceMeshRenderer.transform != sameNameTransform)
                {
                    faceMeshRenderer.transform.parent = prefabInstance.transform;
                    faceMeshRenderer.transform.name = VRChatUtility.AutoBlinkMeshPath;
                }
            }

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
                        prefabInstance: prefabInstance,
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
                        prefabInstance: prefabInstance,
                        fileName: Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh))
                            == VRChatUtility.AutoBlinkMeshPath + ".asset"
                            ? VRChatUtility.AutoBlinkMeshPath + "-" + VRChatUtility.AutoBlinkMeshPath + ".asset"
                            : ""
                    );
                alreadyDuplicatedMeshes[mesh] = filter.sharedMesh;
            }
        }

        /// <summary>
        /// プレハブが依存しているマテリアルを複製します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        private static void DuplicateMaterials(GameObject prefabInstance)
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
                        = Duplicator.DuplicateAssetToFolder<Material>(source: material, prefabInstance: prefabInstance);
                }).ToArray();
            }
        }
    }
}
