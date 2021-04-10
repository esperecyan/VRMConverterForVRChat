using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using VRM;

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

                    if (binding.Index >= mesh.blendShapeCount)
                    {
                        continue;
                    }

                    var shapeKeyName = mesh.GetBlendShapeName(binding.Index);
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
        )
        {
            return clips.Select(clip =>
            {
                clip.ShapeKeyValues = clip.ShapeKeyValues.ToDictionary(
                    keySelector: nameAndWeight => nameAndWeight.Key == oldName ? newName : nameAndWeight.Key,
                    elementSelector: nameAndWeight => nameAndWeight.Value
                );
                return clip;
            }).ToList();
        }

        /// <summary>
        /// VRMBlendShapeの一覧を返します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns>取得できない場合には空のリストを返します。</returns>
        internal static IEnumerable<BlendShapeClip> GetBlendShapeClips(Animator avatar)
        {
            return avatar.GetComponent<VRMBlendShapeProxy>()?.BlendShapeAvatar?.Clips ?? new List<BlendShapeClip>();
        }

        /// <summary>
        /// 指定された名前のユーザー定義VRMBlendShapeを取得します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="name"><see cref="BlendShapeClip.BlendShapeName"/> に一致する文字列。</param>
        /// <returns></returns>
        internal static BlendShapeClip GetUserDefinedBlendShapeClip(Animator avatar, string name)
        {
            return VRMUtility.GetUserDefinedBlendShapeClip(VRMUtility.GetBlendShapeClips(avatar), name);
        }

        /// <summary>
        /// 指定された名前のユーザー定義VRMBlendShapeを取得します。
        /// </summary>
        /// <param name="clips"></param>
        /// <param name="name"><see cref="BlendShapeClip.BlendShapeName"/> に一致する文字列。</param>
        /// <returns></returns>
        internal static BlendShapeClip GetUserDefinedBlendShapeClip(IEnumerable<BlendShapeClip> clips, string name)
        {
            return clips.FirstOrDefault(clip => clip.Preset == BlendShapePreset.Unknown && clip.BlendShapeName == name);
        }

        /// <summary>
        /// <see cref="PreviewSceneManager.Bake"/>
        /// </summary>
        /// <remarks>
        /// 以下のメソッドの改変
        /// https://github.com/vrm-c/UniVRM/blob/v0.57.0/Assets/VRM/UniVRM/Scripts/BlendShape/PreviewSceneManager.cs#L208-L273
        ///
        /// MIT License
        /// 
        /// Copyright(c) 2020 VRM Consortium
        /// 
        /// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
        /// 
        /// The above copyright notice and this permission notice (including the next paragraph) shall be included in all copies or substantial portions of the Software.
        /// 
        /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
        /// </remarks>
#pragma warning disable
        internal static void Bake(Material material, IEnumerable<MaterialValueBinding> bindings)
        {
            var item = MaterialItem.Create(material);

            foreach (var x in bindings)
            {
                //Debug.Log("set material");
                PropItem prop;
                if (item.PropMap.TryGetValue(x.ValueName, out prop))
                {
                    var valueName = x.ValueName;
                    if (valueName.EndsWith("_ST_S")
                    || valueName.EndsWith("_ST_T"))
                    {
                        valueName = valueName.Substring(0, valueName.Length - 2);
                    }

                    var value = item.Material.GetVector(valueName);
                    //Debug.LogFormat("{0} => {1}", valueName, x.TargetValue);
                    value += x.TargetValue - x.BaseValue;
                    item.Material.SetColor(valueName, value);
                }
            }
        }
#pragma warning restore
    }
}
