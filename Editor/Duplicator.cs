using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
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
        /// <param name="destinationFolderUnityPath"></param>
        /// <param name="type">アセットの種類。</param>
        /// <param name="fileName">ファイル名。</param>
        /// <returns>「Assets/」から始まるパス。</returns>
        internal static string DetermineAssetPath(string destinationFolderPath, Type type, string fileName = "")
        {
            var destinationFolderUnityPath = UnityPath.FromUnityPath(destinationFolderPath);
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
        /// アセットの種類に応じて、保存先を決定します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <param name="type">アセットの種類。</param>
        /// <param name="fileName">ファイル名。</param>
        /// <returns>「Assets/」から始まるパス。</returns>
        internal static string DetermineAssetPath(GameObject prefabInstance, Type type, string fileName = "")
        {
            return Duplicator.DetermineAssetPath(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance),
                type,
                fileName
            );
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

            return Duplicator.DuplicateAsset(source: source, destinationPath: Duplicator
                .DetermineAssetPath(prefabInstance: prefabInstance, type: typeof(T), fileName: destinationFileName));
        }

        /// <summary>
        /// オブジェクトをプレハブが置かれているディレクトリの直下のフォルダへ保存します。
        /// </summary>
        /// <remarks>
        /// 保存先にすでにアセットが存在していれば上書きし、metaファイルは新規生成しません。
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="ArgumentException">source がすでにアセットとして存在するか、<see cref="AnimatorController"> の場合。</exception>
        /// <param name="source">オブジェクト。</param>
        /// <param name="prefabInstance">プレハブインスタンス。</param>
        /// <param name="destinationFileName">ファイル名がオブジェクト名と異なる場合に指定。</param>
        /// <returns></returns>
        internal static T CreateObjectToFolder<T>(
            T source,
            string prefabPath,
            string destinationFileName = null
        ) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"source はすでにアセットとして「{path}」に存在します。", nameof(T));
            }

            if (source is AnimatorController)
            {
                throw new ArgumentException($"{nameof(AnimatorController)} は上書きできません。", nameof(T));
            }

            var destinationPath = Duplicator.DetermineAssetPath(
                prefabPath,
                typeof(T),
                destinationFileName ?? source.name.EscapeFilePath() + ".asset"
            );

            var destination = AssetDatabase.LoadMainAssetAtPath(destinationPath);
            if (destination)
            {
                EditorUtility.CopySerialized(source, destination);
            }
            else
            {
                AssetDatabase.CreateAsset(source, destinationPath);
            }

            return AssetDatabase.LoadAssetAtPath<T>(destinationPath);
        }

        /// <summary>
        /// オブジェクトをプレハブが置かれているディレクトリの直下のフォルダへ保存します。
        /// </summary>
        /// <remarks>
        /// 保存先にすでにアセットが存在していれば上書きし、metaファイルは新規生成しません。
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="ArgumentException">source がすでにアセットとして存在するか、<see cref="AnimatorController"> の場合。</exception>
        /// <param name="source">オブジェクト。</param>
        /// <param name="prefabInstance">プレハブインスタンス。</param>
        /// <param name="destinationFileName">ファイル名がオブジェクト名と異なる場合に指定。</param>
        /// <returns></returns>
        internal static T CreateObjectToFolder<T>(
            T source,
            GameObject prefabInstance,
            string destinationFileName = null
        ) where T : UnityEngine.Object
        {
            return Duplicator.CreateObjectToFolder<T>(
                source,
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance),
                destinationFileName
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
            var newInstance = UnityEngine.Object.Instantiate(instance);
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
        private static T DuplicateAsset<T>(T source, string destinationPath) where T : UnityEngine.Object
        {
            var sourceUnityPath = UnityPath.FromAsset(source);
            UnityEngine.Object destination = AssetDatabase.LoadMainAssetAtPath(destinationPath);
            var copied = false;
            if (destination)
            {
                if (AssetDatabase.IsNativeAsset(source) && !(source is AnimatorController)
                    || !sourceUnityPath.IsUnderAssetsFolder)
                {
                    EditorUtility.CopySerialized(source, destination);
                }
                else
                {
                    var sourceFullPath = sourceUnityPath.FullPath;
                    var destinationFullPath = destinationPath.AssetPathToFullPath();
                    if (File.GetLastWriteTime(sourceFullPath) != File.GetLastWriteTime(destinationFullPath))
                    {
                        File.Copy(
                            sourceFileName: sourceFullPath,
                            destFileName: destinationFullPath,
                            overwrite: true
                        );
                        AssetDatabase.ImportAsset(destinationPath);
                        copied = true;
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

            var destinationAsset = AssetDatabase.LoadAssetAtPath<T>(destinationPath);
            if (copied)
            {
                EditorUtility.SetDirty(destinationAsset);
                AssetDatabase.SaveAssets();
            }
            return destinationAsset;
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
        )
        {
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
        )
        {
            SkinnedMeshRenderer faceMeshRenderer
                = combineMeshesAndSubMeshes ? null : Duplicator.GetFaceMeshRenderer(prefabInstance: prefabInstance);

            Transform sameNameTransform = prefabInstance.transform.Find(VRChatUtility.AutoBlinkMeshPath);
            if (sameNameTransform && (combineMeshesAndSubMeshes || faceMeshRenderer.transform != sameNameTransform))
            {
                sameNameTransform.name += "-" + VRChatUtility.AutoBlinkMeshPath;
            }

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstance);
            if (combineMeshesAndSubMeshes)
            {
                CombineMeshesAndSubMeshes.Combine(
                    root: prefabInstance,
                    notCombineRendererObjectNames: notCombineRendererObjectNames,
                    destinationObjectName: VRChatUtility.AutoBlinkMeshPath
                );
            }
            else
            {
                if (faceMeshRenderer.transform != sameNameTransform)
                {
                    faceMeshRenderer.transform.parent = prefabInstance.transform;
                    faceMeshRenderer.transform.name = VRChatUtility.AutoBlinkMeshPath;
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(prefabInstance, prefabPath, InteractionMode.AutomatedAction);

            var alreadyDuplicatedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (combineMeshesAndSubMeshes && renderer.name == VRChatUtility.AutoBlinkMeshPath)
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
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                {
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
