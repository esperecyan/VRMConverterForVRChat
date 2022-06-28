using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UniGLTF.MeshUtility;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// 指定したオブジェクト階下のメッシュを、指定したオブジェクト直下へ結合します。その際、マテリアルが同一であるサブメッシュ (マテリアルスロット) を結合します。
    /// </summary>
    public class CombineMeshesAndSubMeshes
    {
        /// <summary>
        /// メッシュを結合します。
        /// </summary>
        /// <param name="root"></param>
        /// <param name="notCombineRendererObjectNames">結合しないメッシュレンダラーのオブジェクト名。</param>
        /// <param name="destinationObjectName">結合したメッシュのオブジェクト名。</param>
        /// <param name="savingAsAsset">アセットとして保存しないなら <c>true</c> を指定。</param>
        /// <returns></returns>
        public static SkinnedMeshRenderer Combine(
            GameObject root,
            IEnumerable<string> notCombineRendererObjectNames,
            string destinationObjectName,
            bool savingAsAsset = true
        )
        {
            CombineMeshesAndSubMeshes.MakeAllVerticesHaveWeights(
                root: root,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );

            return CombineMeshesAndSubMeshes.CombineAllMeshes(
                root: root,
                destinationObjectName: destinationObjectName,
                notCombineRendererObjectNames: notCombineRendererObjectNames,
                savingAsAsset: savingAsAsset
            );
        }

        /// <summary>
        /// すべてのメッシュの全頂点にウェイトが設定された状態にします。
        /// </summary>
        /// <param name="root"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        private static void MakeAllVerticesHaveWeights(
            GameObject root,
            IEnumerable<string> notCombineRendererObjectNames
        )
        {
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (notCombineRendererObjectNames.Contains(renderer.name)
                    || renderer.bones.Length > 1
                    || renderer.bones.Length > 0 && renderer.bones[0] != renderer.transform)
                {
                    continue;
                }

                CombineMeshesAndSubMeshes.MakeVerticesHaveWeight(renderer: renderer);
            }

            foreach (var meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
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

                CombineMeshesAndSubMeshes.MakeVerticesHaveWeight(renderer: renderer);
            }
        }

        /// <summary>
        /// 指定されたレンダラーのメッシュの頂点にウェイトが設定された状態にします。
        /// </summary>
        /// <param name="renderer"></param>
        private static void MakeVerticesHaveWeight(SkinnedMeshRenderer renderer)
        {
            Transform bone = renderer.transform.parent;
            renderer.bones = new[] { bone };

            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
            mesh.boneWeights = new BoneWeight[mesh.vertexCount].Select(boneWeight =>
            {
                boneWeight.weight0 = 1;
                return boneWeight;
            }).ToArray();
            mesh.bindposes = new[] { bone.worldToLocalMatrix * renderer.localToWorldMatrix };
            renderer.sharedMesh = mesh;
        }

        /// <summary>
        /// メッシュ、サブメッシュを結合します。
        /// </summary>
        /// <param name="root"></param>
        /// <param name="destinationObjectName"></param>
        /// <param name="notCombineRendererObjectNames"></param>
        /// <param name="savingAsAsset"></param>
        /// <returns></returns>
        private static SkinnedMeshRenderer CombineAllMeshes(
            GameObject root,
            string destinationObjectName,
            IEnumerable<string> notCombineRendererObjectNames,
            bool savingAsAsset
        )
        {
            SkinnedMeshRenderer destinationRenderer = MeshIntegratorUtility.Integrate(
                root,
                MeshEnumerateOption.All,
                excludes: root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(renderer => notCombineRendererObjectNames.Contains(renderer.name))
                    .Select(renderer => renderer.sharedMesh)
            ).IntegratedRenderer;
            destinationRenderer.transform.SetParent(root.transform);

            var animator = root.GetComponent<Animator>();
            if (animator)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips)
                {
                    destinationRenderer.rootBone = hips;
                }
            }

            var rootPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(rootPath))
            {
                rootPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer == destinationRenderer || notCombineRendererObjectNames.Contains(renderer.name))
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
                    var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
                    if (prefabRoot)
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            prefabRoot,
                            PrefabUnpackMode.OutermostRoot,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            destinationRenderer.name = destinationObjectName;
            destinationRenderer.sharedMesh.name = destinationRenderer.name;

            if (savingAsAsset)
            {
                var destinationFolderPath = "Assets";
                if (!string.IsNullOrEmpty(rootPath))
                {
                    destinationFolderPath = Path.ChangeExtension(rootPath, ".Meshes");
                    if (!AssetDatabase.IsValidFolder(destinationFolderPath))
                    {
                        AssetDatabase.CreateFolder(
                            Path.GetDirectoryName(destinationFolderPath),
                            Path.GetFileName(destinationFolderPath)
                        );
                    }
                }

                var destinationPath = destinationFolderPath + "/" + destinationRenderer.sharedMesh.name + ".asset";
                var destination = AssetDatabase.LoadAssetAtPath<Mesh>(destinationPath);
                if (destination)
                {
                    destination.Clear(false);
                    EditorUtility.CopySerialized(destinationRenderer.sharedMesh, destination);
                    destinationRenderer.sharedMesh = destination;
                }
                else
                {
                    AssetDatabase.CreateAsset(destinationRenderer.sharedMesh, destinationPath);
                }
            }

            return destinationRenderer;
        }

        /// <summary>
        /// 当エディタ拡張の名称。
        /// </summary>
        internal const string Name = "CombineMeshesAndSubMeshes.cs";
    }

    /// <summary>
    /// ダイアログ。
    /// </summary>
    public class CombinerWizard : ScriptableWizard
    {
        /// <summary>
        /// 追加するメニューアイテムの、「UnityEditorScripts」メニュー内の位置。
        /// </summary>
        public const int Priority = 22;

        [SerializeField]
        private GameObject root = null;

        [SerializeField]
        private string destinationObjectName = "mesh";

        [SerializeField]
        private List<string> notCombineRendererObjectNames = new List<string>();

        private static void OpenWizard()
        {
            CombinerWizard.Open();
        }

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        [MenuItem("GameObject/UnityEditorScripts/" + CombineMeshesAndSubMeshes.Name, false, CombinerWizard.Priority)]
        private static void Open()
        {
            var wizard = DisplayWizard<CombinerWizard>(CombineMeshesAndSubMeshes.Name, "Combine");
            wizard.root = Selection.activeObject as GameObject;
        }

        protected override bool DrawWizardGUI()
        {
            base.DrawWizardGUI();
            this.isValid = true;

            if (!this.root)
            {
                this.isValid = false;
                return true;
            }

            if (string.IsNullOrEmpty(this.destinationObjectName))
            {
                EditorGUILayout.HelpBox("「Destination Object Name」を入力してください。", MessageType.Error);
                this.isValid = false;
            }

            IEnumerable<string> notCombineRendererObjectNames = this.notCombineRendererObjectNames.Except(new[] { "" });
            if (notCombineRendererObjectNames.Count() == 0)
            {
                return true;
            }

            IEnumerable<string> names = notCombineRendererObjectNames.Except(
                this.root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Concat<Component>(this.root.GetComponentsInChildren<MeshRenderer>())
                    .Select(renderer => renderer.name)
            );

            if (names.Count() == 0)
            {
                return true;
            }

            EditorGUILayout.HelpBox(string.Join(separator: "\n• ", value: new[] { "レンダラーが設定されたGameObjectのうち、以下の名前を持つものは存在しません。" }
                .Concat(names).ToArray()), MessageType.Warning);

            return true;
        }

        private void OnWizardCreate()
        {
            CombineMeshesAndSubMeshes.Combine(
                root: this.root,
                notCombineRendererObjectNames: this.notCombineRendererObjectNames.Except(new[] { "" }),
                destinationObjectName: this.destinationObjectName
            );

            EditorUtility.DisplayDialog(
                CombineMeshesAndSubMeshes.Name,
                "メッシュ、およびマテリアルが同一であるサブメッシュの結合が完了しました。",
                "OK"
            );
        }
    }
}
