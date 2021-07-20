using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;
using Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM;
using Esperecyan.Unity.VRMConverterForVRChat.Components;
using VRM;
using UniGLTF;
using UniGLTF.MeshUtility;
using BlendShape = UniGLTF.BlendShape;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// メッシュの操作。
    /// </summary>
    internal static class SkinnedMeshUtility
    {
        /// <summary>
        /// メッシュをheadボーン(およびその子孫)のウェイトが載ったものとそれ以外に分割し、元のGameObjectは削除します。
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="renderer"></param>
        internal static void SplitMeshIntoHeadAndBody(GameObject instance, SkinnedMeshRenderer renderer)
        {
            var head = instance.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var headBoneIndices = renderer.bones.Select((bone, index) => (index, bone))
                .Where(indexBonePair => indexBonePair.bone == head || indexBonePair.bone.IsChildOf(head))
                .Select(indexBonePair => indexBonePair.index);

            foreach (var (name, indices) in new Dictionary<string, IEnumerable<int>>()
            {
                { "vrm-head-mesh", headBoneIndices },
                { "vrm-body-mesh", renderer.bones.Select((bone, index) => index).Except(headBoneIndices) },
            })
            {
                var headObject = new GameObject(name);
                headObject.transform.SetParent(instance.transform);
                var headRenderer = headObject.AddComponent<SkinnedMeshRenderer>();
                headRenderer.sharedMesh = BoneMeshEraser.CreateErasedMesh(renderer.sharedMesh, indices.ToArray());
                headRenderer.sharedMesh.name = name;
            }

            Object.DestroyImmediate(renderer.gameObject);
        }

        /// <summary>
        /// 指定したメッシュのすべてのシェイプキーを取得します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        /// <returns></returns>
        internal static IEnumerable<BlendShape> GetAllShapeKeys(Mesh mesh, bool useShapeKeyNormalsAndTangents)
        {
            var shapeKeys = new List<BlendShape>();

            var meshVertexCount = mesh.vertexCount;
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var deltaVertices = new Vector3[meshVertexCount];
                var deltaNormals = new Vector3[meshVertexCount];
                var deltaTangents = new Vector3[meshVertexCount];

                mesh.GetBlendShapeFrameVertices(
                    i,
                    0,
                    deltaVertices,
                    useShapeKeyNormalsAndTangents ? deltaNormals : null,
                    useShapeKeyNormalsAndTangents ? deltaTangents : null
                );

                shapeKeys.Add(new BlendShape(name: mesh.GetBlendShapeName(i))
                {
                    Positions = deltaVertices.ToList(),
                    Normals = deltaNormals.ToList(),
                    Tangents = deltaTangents.ToList(),
                });
            }

            return shapeKeys;
        }

        /// <summary>
        /// nesessaryShapeKeysで指定されたシェイプキーから法線・接線を削除し、それ以外のシェイプキーは削除します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="nesessaryShapeKeys"></param>
        /// <returns></returns>
        internal static void CleanUpShapeKeys(Mesh mesh, IEnumerable<string> nesessaryShapeKeys)
        {
            var shapeKeys = SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents: false);
            mesh.ClearBlendShapes();
            foreach (var name in nesessaryShapeKeys)
            {
                var shapeKey = shapeKeys.FirstOrDefault(key => key.Name == name);
                if (shapeKey == null)
                {
                    continue;
                }

                mesh.AddBlendShapeFrame(
                    shapeKey.Name,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    shapeKey.Positions.ToArray(),
                    shapeKey.Normals.ToArray(),
                    shapeKey.Tangents.ToArray()
                );
            }

        }
    }
}
