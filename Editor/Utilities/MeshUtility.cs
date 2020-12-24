/// 当ファイルは、MIT Licenseで提供されている下記ファイルを改変したもの。
/// https://github.com/vrm-c/UniVRM/blob/v0.62.0/Assets/MeshUtility/Editor/MeshUtility.cs
///
/// MIT License
/// 
/// Copyright(c) 2020 VRM Consortium
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice (including the next paragraph) shall be included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#pragma warning disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    internal class MeshUtility
    {
        private const string ASSET_SUFFIX = ".mesh.asset";
        private static readonly Vector3 ZERO_MOVEMENT = Vector3.zero;

        private enum BlendShapeLogic
        {
            WithBlendShape,
            WithoutBlendShape,
        }

        internal static void SeparationProcessing(GameObject go)
        {
            var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                {
                    SeparatePolyWithBlendShape(skinnedMeshRenderer);
                }
            }
        }

        private static void SeparatePolyWithBlendShape(SkinnedMeshRenderer skinnedMeshRendererInput)
        {
            var indicesUsedByBlendShape = new Dictionary<int, int>();
            var mesh = skinnedMeshRendererInput.sharedMesh;

            // retrieve the original BlendShape data
            for (int i = 0; i < mesh.blendShapeCount; ++i)
            {
                var deltaVertices = new Vector3[mesh.vertexCount];
                var deltaNormals = new Vector3[mesh.vertexCount];
                var deltaTangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

                for (int j = 0; j < deltaVertices.Length; j++)
                {
                    if (!deltaVertices[j].Equals(ZERO_MOVEMENT))
                    {
                        if (!indicesUsedByBlendShape.Values.Contains(j))
                        {
                            indicesUsedByBlendShape.Add(indicesUsedByBlendShape.Count, j);
                        }
                    }
                }
            }

            var subMeshCount = mesh.subMeshCount;
            var submeshesWithBlendShape = new Dictionary<int, int[]>();
            var submeshesWithoutBlendShape = new Dictionary<int, int[]>();
            var vertexIndexWithBlendShape = new Dictionary<int, int>();
            var vertexCounterWithBlendShape = 0;
            var vertexIndexWithoutBlendShape = new Dictionary<int, int>();
            var vertexCounterWithoutBlendShape = 0;

            // check blendshape's vertex index from submesh
            for (int i = 0; i < subMeshCount; i++)
            {
                var triangle = mesh.GetTriangles(i);
                var submeshWithBlendShape = new List<int>();
                var submeshWithoutBlendShape = new List<int>();

                for (int j = 0; j < triangle.Length; j += 3)
                {
                    if (indicesUsedByBlendShape.Values.Contains(triangle[j]) ||
                        indicesUsedByBlendShape.Values.Contains(triangle[j + 1]) ||
                        indicesUsedByBlendShape.Values.Contains(triangle[j + 2]))
                    {
                        BuildNewTriangleList(vertexIndexWithBlendShape, triangle, j, submeshWithBlendShape, ref vertexCounterWithBlendShape);
                    }
                    else
                    {
                        BuildNewTriangleList(vertexIndexWithoutBlendShape, triangle, j, submeshWithoutBlendShape, ref vertexCounterWithoutBlendShape);
                    }
                }
                if (submeshWithBlendShape.Count > 0)
                    submeshesWithBlendShape.Add(i, submeshWithBlendShape.ToArray());
                if (submeshWithoutBlendShape.Count > 0)
                    submeshesWithoutBlendShape.Add(i, submeshWithoutBlendShape.ToArray());
                ;
            }

            // check if any BlendShape exists
            if (submeshesWithoutBlendShape.Count > 0)
            {
                // put the mesh without BlendShape in a new SkinnedMeshRenderer
                var srcGameObject = skinnedMeshRendererInput.gameObject;
                var srcTransform = skinnedMeshRendererInput.transform.parent;
                var targetObjectForMeshWithoutBS = GameObject.Instantiate(srcGameObject);
                targetObjectForMeshWithoutBS.name = srcGameObject.name + "_WithoutBlendShape";
                targetObjectForMeshWithoutBS.transform.SetParent(srcTransform);
                var skinnedMeshRendererWithoutBS = targetObjectForMeshWithoutBS.GetComponent<SkinnedMeshRenderer>();

                // build meshes with/without BlendShape
                BuildNewMesh(skinnedMeshRendererInput, vertexIndexWithBlendShape, submeshesWithBlendShape, BlendShapeLogic.WithBlendShape);
                BuildNewMesh(skinnedMeshRendererWithoutBS, vertexIndexWithoutBlendShape, submeshesWithoutBlendShape, BlendShapeLogic.WithoutBlendShape);
            }
        }

        private static void BuildNewTriangleList(Dictionary<int, int> newVerticesListLookUp, int[] triangleList, int index,
                                                 List<int> newTriangleList, ref int vertexCounter)
        {
            // build new vertex list and triangle list
            // vertex 1
            if (!newVerticesListLookUp.Keys.Contains(triangleList[index]))
            {
                newVerticesListLookUp.Add(triangleList[index], vertexCounter);
                newTriangleList.Add(vertexCounter);
                vertexCounter++;
            }
            else
            {
                var newVertexIndex = newVerticesListLookUp[triangleList[index]];
                newTriangleList.Add(newVertexIndex);
            }
            // vertex 2
            if (!newVerticesListLookUp.Keys.Contains(triangleList[index + 1]))
            {
                newVerticesListLookUp.Add(triangleList[index + 1], vertexCounter);
                newTriangleList.Add(vertexCounter);
                vertexCounter++;
            }
            else
            {
                var newVertexIndex = newVerticesListLookUp[triangleList[index + 1]];
                newTriangleList.Add(newVertexIndex);
            }
            // vertex 3
            if (!newVerticesListLookUp.Keys.Contains(triangleList[index + 2]))
            {
                newVerticesListLookUp.Add(triangleList[index + 2], vertexCounter);
                newTriangleList.Add(vertexCounter);
                vertexCounter++;
            }
            else
            {
                var newVertexIndex = newVerticesListLookUp[triangleList[index + 2]];
                newTriangleList.Add(newVertexIndex);
            }
        }

        private static void BuildNewMesh(SkinnedMeshRenderer skinnedMeshRenderer, Dictionary<int, int> newIndexLookUpDict,
                                         Dictionary<int, int[]> subMeshes, BlendShapeLogic blendShapeLabel)
        {
            // get original mesh data
            var materialList = new List<Material>();
            skinnedMeshRenderer.GetSharedMaterials(materialList);
            var mesh = skinnedMeshRenderer.sharedMesh;
            var meshVertices = mesh.vertices;
            var meshNormals = mesh.normals;
            var meshTangents = mesh.tangents;
            var meshColors = mesh.colors;
            var meshBoneWeights = mesh.boneWeights;
            var meshUVs = mesh.uv;

            // build new mesh
            var materialListNew = new List<Material>();
            var newMesh = new Mesh();

            if (mesh.vertexCount > ushort.MaxValue)
            {
#if UNITY_2017_3_OR_NEWER
                Debug.LogFormat("exceed 65535 vertices: {0}", mesh.vertexCount);
                newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#else
                throw new NotImplementedException(String.Format("exceed 65535 vertices: {0}", integrator.Positions.Count.ToString()));
#endif
            }

            var newDataLength = newIndexLookUpDict.Count;
            var newIndexLookUp = newIndexLookUpDict.Keys.ToArray();

            newMesh.vertices = newIndexLookUp.Select(x => meshVertices[x]).ToArray();
            if (meshNormals.Length > 0)
                newMesh.normals = newIndexLookUp.Select(x => meshNormals[x]).ToArray();
            if (meshTangents.Length > 0)
                newMesh.tangents = newIndexLookUp.Select(x => meshTangents[x]).ToArray();
            if (meshColors.Length > 0)
                newMesh.colors = newIndexLookUp.Select(x => meshColors[x]).ToArray();
            if (meshBoneWeights.Length > 0)
                newMesh.boneWeights = newIndexLookUp.Select(x => meshBoneWeights[x]).ToArray();
            if (meshUVs.Length > 0)
                newMesh.uv = newIndexLookUp.Select(x => meshUVs[x]).ToArray();
            newMesh.bindposes = mesh.bindposes;

            // add BlendShape data
            if (blendShapeLabel == BlendShapeLogic.WithBlendShape)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    // get original BlendShape data
                    var srcVertices = new Vector3[mesh.vertexCount];
                    var srcNormals = new Vector3[mesh.vertexCount];
                    var srcTangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(i, 0, srcVertices, srcNormals, srcTangents);

                    // declare the size for the destination array
                    var dstVertices = new Vector3[newDataLength];
                    var dstNormals = new Vector3[newDataLength];
                    var dstTangents = new Vector3[newDataLength];

                    dstVertices = newIndexLookUp.Select(x => srcVertices[x]).ToArray();
                    dstNormals = newIndexLookUp.Select(x => srcNormals[x]).ToArray();
                    dstTangents = newIndexLookUp.Select(x => srcTangents[x]).ToArray();
                    newMesh.AddBlendShapeFrame(mesh.GetBlendShapeName(i), mesh.GetBlendShapeFrameWeight(i, 0),
                                               dstVertices, dstNormals, dstTangents);
                }
            }

            newMesh.subMeshCount = subMeshes.Count;
            var cosMaterialIndex = subMeshes.Keys.ToArray();

            // build material list
            for (int i = 0; i < subMeshes.Count; i++)
            {
                newMesh.SetTriangles(subMeshes[cosMaterialIndex[i]], i);
                materialListNew.Add(materialList[cosMaterialIndex[i]]);
            }
            skinnedMeshRenderer.sharedMaterials = materialListNew.ToArray();
            skinnedMeshRenderer.sharedMesh = newMesh;
        }
    }
}
