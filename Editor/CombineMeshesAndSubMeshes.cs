using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Esperecyan.Unity.CombineMeshesAndSubMeshes
{
    /// <summary>
    /// 指定したオブジェクト階下のメッシュを、指定したオブジェクト直下へ結合します。その際、マテリアルが同一であるサブメッシュ (マテリアルスロット) を結合します。
    /// </summary>
    /// <remarks>
    /// 動作確認バージョン: Unity 2017.4.28f1
    /// ライセンス: Mozilla Public License 2.0 (MPL-2.0) <https://spdx.org/licenses/MPL-2.0.html>
    /// 配布元: <https://gist.github.com/esperecyan/426824a84efc9c6e0bc6f72731a41a5b>
    /// </remarks>
    public class CombineMeshesAndSubMeshes
    {
        /// <summary>
        /// 当エディタ拡張のバージョン。
        /// </summary>
        /// <remarks>
        /// 0.1.0 (2019-07-15)
        ///     『VRM Converter for VRChat』から分離、手直し
        /// </remarks>
        public const string Version = "0.1.0";

        /// <summary>
        /// メッシュを結合します。
        /// </summary>
        /// <param name="root"></param>
        /// <param name="notCombineRendererObjectNames">結合しないメッシュレンダラーのオブジェクト名。</param>
        /// <param name="destinationObjectName">結合したメッシュのオブジェクト名。</param>
        /// <returns></returns>
        public static SkinnedMeshRenderer Combine(
            GameObject root,
            IEnumerable<string> notCombineRendererObjectNames,
            string destinationObjectName
        )
        {
            CombineMeshesAndSubMeshes.MakeAllVerticesHaveWeights(
                root: root,
                notCombineRendererObjectNames: notCombineRendererObjectNames
            );

            return CombineMeshesAndSubMeshes.CombineAllMeshes(
                root: root,
                destinationObjectName: destinationObjectName,
                notCombineRendererObjectNames: notCombineRendererObjectNames
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

            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh) as Mesh;
            mesh.boneWeights = new BoneWeight[mesh.vertexCount].Select(boneWeight => {
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
        /// <returns></returns>
        private static SkinnedMeshRenderer CombineAllMeshes(
            GameObject root,
            string destinationObjectName,
            IEnumerable<string> notCombineRendererObjectNames
        )
        {
            SkinnedMeshRenderer destinationRenderer
                = MeshIntegrator.Integrate(go: root, notCombineRendererObjectNames: notCombineRendererObjectNames);

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
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            destinationRenderer.name = destinationObjectName;
            destinationRenderer.sharedMesh.name = destinationRenderer.name;


            string rootPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(rootPath))
            {
                var prefab = PrefabUtility.GetPrefabParent(root);
                if (prefab)
                {
                    rootPath = AssetDatabase.GetAssetPath(prefab);
                }
            }

            string destinationFolderPath = "Assets";
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

            string destinationPath = destinationFolderPath + "/" + destinationRenderer.sharedMesh.name + ".asset";
            var destination = AssetDatabase.LoadAssetAtPath<Mesh>(destinationPath);
            if (destination)
            {
                EditorUtility.CopySerialized(destinationRenderer.sharedMesh, destination);
                destinationRenderer.sharedMesh = destination;
            }
            else
            {
                AssetDatabase.CreateAsset(destinationRenderer.sharedMesh, destinationPath);
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
    public class Wizard : ScriptableWizard
    {
        /// <summary>
        /// 追加するメニューアイテムの、「UnityEditorScripts」メニュー内の位置。
        /// </summary>
        public const int Priority = 22;

        private const string NameAndVersion = CombineMeshesAndSubMeshes.Name + "-" + CombineMeshesAndSubMeshes.Version;

        [SerializeField]
        private GameObject root = null;

        [SerializeField]
        private string destinationObjectName = "mesh";

        [SerializeField]
        private List<string> notCombineRendererObjectNames = new List<string>();

        private static void OpenWizard()
        {
            Wizard.Open();
        }

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        [MenuItem("GameObject/UnityEditorScripts/" + Wizard.NameAndVersion, false, Wizard.Priority)]
        private static void Open()
        {
            var wizard = DisplayWizard<Wizard>(NameAndVersion, "Combine");
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
                root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Concat<Component>(root.GetComponentsInChildren<MeshRenderer>())
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
                Wizard.NameAndVersion,
                "メッシュ、およびマテリアルが同一であるサブメッシュの結合が完了しました。",
                "OK"
            );
        }
    }

    /// <summary>
    /// 複数のメッシュをまとめる
    /// </summary>
    /// <remarks>
    /// MIT Licenseで提供されている下記クラスの次のメソッドを改変したもの。
    /// • <see cref="MeshIntegrator.Integrate"/>
    /// • <see cref="MeshIntegrator.AddBlendShapesToMesh"/>
    /// • <see cref="MeshIntegrator.EnumerateRenderer"/>
    /// • <see cref="MeshIntegrator._Integrate"/>
    /// <https://github.com/vrm-c/UniVRM/blob/v0.53.0/Assets/VRM/UniVRM/Editor/SkinnedMeshUtility/MeshIntegrator.cs#L377>
    /// 
    /// MIT License
    /// 
    /// Copyright(c) 2018 DWANGO Co., Ltd. for UniVRM
    /// Copyright (c) 2018 ousttrue for UniGLTF, UniHumanoid
    /// Copyright (c) 2018 Masataka SUMI for MToon
    /// 
    /// Permission is hereby granted, free of charge, to any person obtaining a copy
    /// of this software and associated documentation files (the "Software"), to deal
    /// in the Software without restriction, including without limitation the rights
    /// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    /// copies of the Software, and to permit persons to whom the Software is
    /// furnished to do so, subject to the following conditions:
    /// 
    /// The above copyright notice and this permission notice shall be included in all
    /// copies or substantial portions of the Software.
    /// 
    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    /// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    /// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    /// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    /// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    /// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    /// SOFTWARE.
    /// </remarks>
    internal static class MeshIntegrator
    {
        const string ASSET_SUFFIX = ".mesh.asset";
        const string ASSET_WITH_BLENDSHAPE_SUFFIX = ".blendshape.asset";

        private static bool ExportValidate()
        {
            return Selection.activeObject != null && Selection.activeObject is GameObject;
        }

        private static void ExportFromMenu()
        {
            var go = Selection.activeObject as GameObject;

            Integrate(go);
        }

        public static SkinnedMeshRenderer Integrate(GameObject go, IEnumerable<string> notCombineRendererObjectNames = null)
        {
            return _Integrate(go, notCombineRendererObjectNames);
        }

        struct SubMesh
        {
            public List<int> Indices;
            public Material Material;
        }

        class BlendShape
        {
            public int VertexOffset;
            public string Name;
            public float FrameWeight;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector3[] Tangents;
        }

        class Integrator
        {
            //            public List<SkinnedMeshRenderer> Renderers { get; private set; }
            public List<Vector3> Positions { get; private set; }
            public List<Vector3> Normals { get; private set; }
            public List<Vector2> UV { get; private set; }
            public List<Vector4> Tangents { get; private set; }
            public List<BoneWeight> BoneWeights { get; private set; }

            public List<SubMesh> SubMeshes
            {
                get;
                private set;
            }

            public List<Matrix4x4> BindPoses { get; private set; }
            public List<Transform> Bones { get; private set; }

            public List<BlendShape> BlendShapes { get; private set; }
            public void AddBlendShapesToMesh(Mesh mesh)
            {
                Dictionary<string, BlendShape> map = new Dictionary<string, BlendShape>();

                foreach (var x in BlendShapes)
                {
                    BlendShape bs = null;
                    if (!map.TryGetValue(x.Name, out bs))
                    {
                        bs = new BlendShape();
                        bs.Positions = new Vector3[Positions.Count];
                        bs.Normals = new Vector3[Positions.Count];
                        bs.Tangents = new Vector3[Positions.Count];
                        bs.Name = x.Name;
                        bs.FrameWeight = x.FrameWeight;
                        map.Add(x.Name, bs);
                    }

                    var j = x.VertexOffset;
                    for (int i = 0; i < x.Positions.Length; ++i, ++j)
                    {
                        bs.Positions[j] = x.Positions[i];
                        bs.Normals[j] = x.Normals[i];
                        bs.Tangents[j] = x.Tangents[i];
                    }
                }

                foreach (var kv in map)
                {
                    //Debug.LogFormat("AddBlendShapeFrame: {0}", kv.Key);
                    mesh.AddBlendShapeFrame(kv.Key, kv.Value.FrameWeight,
                        kv.Value.Positions, kv.Value.Normals, kv.Value.Tangents);
                }
            }

            public Integrator()
            {
                //                Renderers = new List<SkinnedMeshRenderer>();

                Positions = new List<Vector3>();
                Normals = new List<Vector3>();
                UV = new List<Vector2>();
                Tangents = new List<Vector4>();
                BoneWeights = new List<BoneWeight>();

                SubMeshes = new List<SubMesh>();

                BindPoses = new List<Matrix4x4>();
                Bones = new List<Transform>();

                BlendShapes = new List<BlendShape>();
            }

            static BoneWeight AddBoneIndexOffset(BoneWeight bw, int boneIndexOffset)
            {
                if (bw.weight0 > 0) bw.boneIndex0 += boneIndexOffset;
                if (bw.weight1 > 0) bw.boneIndex1 += boneIndexOffset;
                if (bw.weight2 > 0) bw.boneIndex2 += boneIndexOffset;
                if (bw.weight3 > 0) bw.boneIndex3 += boneIndexOffset;
                return bw;
            }

            public void Push(MeshRenderer renderer)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogWarningFormat("{0} has no mesh filter", renderer.name);
                    return;
                }
                var mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    Debug.LogWarningFormat("{0} has no mesh", renderer.name);
                    return;
                }

                var indexOffset = Positions.Count;
                var boneIndexOffset = Bones.Count;

                Positions.AddRange(mesh.vertices
                    .Select(x => renderer.transform.TransformPoint(x))
                );
                Normals.AddRange(mesh.normals
                    .Select(x => renderer.transform.TransformVector(x))
                );
                UV.AddRange(mesh.uv);
                Tangents.AddRange(mesh.tangents
                    .Select(t =>
                    {
                        var v = renderer.transform.TransformVector(t.x, t.y, t.z);
                        return new Vector4(v.x, v.y, v.z, t.w);
                    })
                );

                var self = renderer.transform;
                var bone = self.parent;
                if (bone == null)
                {
                    Debug.LogWarningFormat("{0} is root gameobject.", self.name);
                    return;
                }
                var bindpose = bone.worldToLocalMatrix;

                BoneWeights.AddRange(Enumerable.Range(0, mesh.vertices.Length)
                    .Select(x => new BoneWeight()
                    {
                        boneIndex0 = Bones.Count,
                        weight0 = 1,
                    })
                );

                BindPoses.Add(bindpose);
                Bones.Add(bone);

                for (int i = 0; i < mesh.subMeshCount; ++i)
                {
                    var indices = mesh.GetIndices(i).Select(x => x + indexOffset);
                    var mat = renderer.sharedMaterials[i];
                    var sameMaterialSubMeshIndex = SubMeshes.FindIndex(x => ReferenceEquals(x.Material, mat));
                    if (sameMaterialSubMeshIndex >= 0)
                    {
                        SubMeshes[sameMaterialSubMeshIndex].Indices.AddRange(indices);
                    }
                    else
                    {
                        SubMeshes.Add(new SubMesh
                        {
                            Indices = indices.ToList(),
                            Material = mat,
                        });
                    }
                }
            }

            public void Push(SkinnedMeshRenderer renderer)
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    Debug.LogWarningFormat("{0} has no mesh", renderer.name);
                    return;
                }

                //                Renderers.Add(renderer);

                var indexOffset = Positions.Count;
                var boneIndexOffset = Bones.Count;

                Positions.AddRange(mesh.vertices);
                Normals.AddRange(mesh.normals);
                UV.AddRange(mesh.uv);
                Tangents.AddRange(mesh.tangents);

                if (mesh.vertexCount == mesh.boneWeights.Length)
                {
                    BoneWeights.AddRange(mesh.boneWeights.Select(x => AddBoneIndexOffset(x, boneIndexOffset)).ToArray());
                }
                else
                {
                    BoneWeights.AddRange(Enumerable.Range(0, mesh.vertexCount).Select(x => new BoneWeight()).ToArray());
                }

                BindPoses.AddRange(mesh.bindposes);
                Bones.AddRange(renderer.bones);

                for (int i = 0; i < mesh.subMeshCount; ++i)
                {
                    var indices = mesh.GetIndices(i).Select(x => x + indexOffset);
                    var mat = renderer.sharedMaterials[i];
                    var sameMaterialSubMeshIndex = SubMeshes.FindIndex(x => ReferenceEquals(x.Material, mat));
                    if (sameMaterialSubMeshIndex >= 0)
                    {
                        SubMeshes[sameMaterialSubMeshIndex].Indices.AddRange(indices);
                    }
                    else
                    {
                        SubMeshes.Add(new SubMesh
                        {
                            Indices = indices.ToList(),
                            Material = mat,
                        });
                    }
                }

                for (int i = 0; i < mesh.blendShapeCount; ++i)
                {
                    var positions = (Vector3[])mesh.vertices.Clone();
                    var normals = (Vector3[])mesh.normals.Clone();
                    var tangents = mesh.tangents.Select(x => (Vector3)x).ToArray();

                    mesh.GetBlendShapeFrameVertices(i, 0, positions, normals, tangents);
                    BlendShapes.Add(new BlendShape
                    {
                        VertexOffset = indexOffset,
                        FrameWeight = mesh.GetBlendShapeFrameWeight(i, 0),
                        Name = mesh.GetBlendShapeName(i),
                        Positions = positions,
                        Normals = normals,
                        Tangents = tangents,
                    });
                }
            }
        }

        static IEnumerable<Transform> Traverse(Transform parent)
        {
            if (parent.gameObject.activeSelf)
            {
                yield return parent;

                foreach (Transform child in parent)
                {
                    foreach (var x in Traverse(child))
                    {
                        yield return x;
                    }
                }
            }
        }

        static public IEnumerable<SkinnedMeshRenderer> EnumerateRenderer(Transform root)
        {
            foreach (var x in Traverse(root))
            {
                var renderer = x.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null)
                {
                    if (renderer.sharedMesh != null)
                    {
                        if (renderer.gameObject.activeSelf)
                        {
                            yield return renderer;
                        }
                    }
                }
            }
        }

        static IEnumerable<MeshRenderer> EnumerateMeshRenderer(Transform root)
        {
            foreach (var x in Traverse(root))
            {
                var renderer = x.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var filter = x.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && renderer.gameObject.activeSelf)
                    {
                        yield return renderer;
                    }
                }
            }
        }

        static IEnumerable<Transform> Ancestors(Transform self)
        {
            yield return self;

            if (self.parent != null)
            {
                foreach (var x in Ancestors(self.parent))
                {
                    yield return x;
                }
            }
        }

        static SkinnedMeshRenderer _Integrate(GameObject go, IEnumerable<string> notCombineRendererObjectNames)
        {
            var meshNode = new GameObject();
            meshNode.name = Random.Range(int.MinValue, int.MaxValue).ToString();
            meshNode.transform.SetParent(go.transform, false);

            var renderers = EnumerateRenderer(go.transform).ToArray();

            // レンダラから情報を集める
            var integrator = new Integrator();
            foreach (var x in renderers.Where(renderer => !notCombineRendererObjectNames.Contains(renderer.name)))
            {
                integrator.Push(x);
            }

            var mesh = new Mesh();
            mesh.name = "integrated";

            if (integrator.Positions.Count > ushort.MaxValue)
            {
#if UNITY_2017_3_OR_NEWER
                Debug.LogFormat("exceed 65535 vertices: {0}", integrator.Positions.Count);
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#else
                throw new NotImplementedException(String.Format("exceed 65535 vertices: {0}", integrator.Positions.Count.ToString()));
#endif
            }

            mesh.vertices = integrator.Positions.ToArray();
            mesh.normals = integrator.Normals.ToArray();
            mesh.uv = integrator.UV.ToArray();
            mesh.tangents = integrator.Tangents.ToArray();
            mesh.boneWeights = integrator.BoneWeights.ToArray();
            mesh.subMeshCount = integrator.SubMeshes.Count;
            for (var i = 0; i < integrator.SubMeshes.Count; ++i)
            {
                mesh.SetIndices(integrator.SubMeshes[i].Indices.ToArray(), MeshTopology.Triangles, i);
            }
            mesh.bindposes = integrator.BindPoses.ToArray();

            integrator.AddBlendShapesToMesh(mesh);

            var integrated = meshNode.AddComponent<SkinnedMeshRenderer>();
            integrated.sharedMesh = mesh;
            integrated.sharedMaterials = integrator.SubMeshes.Select(x => x.Material).ToArray();
            integrated.bones = integrator.Bones.ToArray();

            return integrated;
        }
    }
}
