using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using VRM;
using UniGLTF;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRM関連の処理など。
    /// </summary>
    public class VRMUtility
    {
        /// <summary>
        /// <see cref="BlendShapeBinding.Weight"/>の最高値。
        /// </summary>
        internal static readonly float MaxBlendShapeBindingWeight = 100;

        /// <summary>
        /// VRMのブレンドシェイプを、シェイプキー名ベースで取得します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        public static IEnumerable<VRMBlendShapeClip> GetAllVRMBlendShapeClips(GameObject avatar)
        {
            var clips = new List<VRMBlendShapeClip>();

            var blendShapeProxy = avatar.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy)
            {
                return clips;
            }

            BlendShapeAvatar blendShapeAvatar = blendShapeProxy.BlendShapeAvatar;
            if (!blendShapeAvatar)
            {
                return clips;
            }

            foreach (BlendShapeClip blendShapeClip in blendShapeAvatar.Clips)
            {
                if (!blendShapeClip)
                {
                    continue;
                }

                var clip = ScriptableObject.CreateInstance<VRMBlendShapeClip>();
                clip.BlendShapeName = blendShapeClip.BlendShapeName;
                clip.Preset = blendShapeClip.Preset;
                clip.Values = blendShapeClip.Values;
                clip.MaterialValues = blendShapeClip.MaterialValues;
                clip.IsBinary = blendShapeClip.IsBinary;
            
                foreach (BlendShapeBinding binding in clip.Values)
                {
                    Transform transform = avatar.transform.Find(binding.RelativePath);
                    if (!transform)
                    {
                        continue;
                    }

                    var renderer = transform.GetComponent<SkinnedMeshRenderer>();
                    if (!renderer)
                    {
                        continue;
                    }

                    Mesh mesh = renderer.sharedMesh;
                    if (!mesh)
                    {
                        continue;
                    }

                    if (binding.Index > mesh.blendShapeCount)
                    {
                        continue;
                    }

                    string shapeKeyName = mesh.GetBlendShapeName(binding.Index);
                    if (clip.ShapeKeyValues.ContainsKey(shapeKeyName))
                    {
                        if (binding.Weight > clip.ShapeKeyValues[shapeKeyName])
                        {
                            clip.ShapeKeyValues[shapeKeyName] = binding.Weight;
                        }
                    }
                    else
                    {
                        clip.ShapeKeyValues.Add(key: shapeKeyName, value: binding.Weight);
                    }
                }

                if (clip.ShapeKeyValues.Count == 0)
                {
                    continue;
                }

                clips.Add(clip);
            }

            return clips;
        }

        /// <summary>
        /// 指定したシェイプキー名を置換します。
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        internal static IEnumerable<VRMBlendShapeClip> ReplaceShapeKeyName(
            IEnumerable<VRMBlendShapeClip> clips,
            string oldName,
            string newName
        ) {
            return clips.Select(clip => {
                clip.ShapeKeyValues = clip.ShapeKeyValues.ToDictionary(
                    keySelector: nameAndWeight => nameAndWeight.Key == oldName ? newName : nameAndWeight.Key,
                    elementSelector: nameAndWeight => nameAndWeight.Value
                );
                return clip;
            });
        }
    }
}