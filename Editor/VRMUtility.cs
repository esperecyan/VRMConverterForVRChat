using System.Linq;
using UnityEngine;
using VRM;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRM関連の処理など。
    /// </summary>
    internal class VRMUtility
    {
        /// <summary>
        /// 指定されたブレンドシェイプの最初の<see cref="BlendShapeBinding"/>について、対応するSkinned Mesh Rendererコンポーネントを返します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <returns></returns>
        internal static SkinnedMeshRenderer GetFirstSkinnedMeshRenderer(GameObject avatar, BlendShapePreset preset)
        {
            var clip = GetBlendShapeClip(avatar: avatar, preset: preset);
            if (!clip)
            {
                return null;
            }

            BlendShapeBinding[] bindings = clip.Values;
            if (bindings.Length == 0)
            {
                return null;
            }

            var binding = bindings[0];
            Transform face = avatar.transform.Find(name: binding.RelativePath);
            if (!face)
            {
                return null;
            }

            return face.GetComponent<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// 指定されたブレンドシェイプの最初の<see cref="BlendShapeBinding"/>について、対応するSkinned Mesh Rendererコンポーネント中のブレンドシェイプ名を返します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <returns>対応するブレンドシェイプ名が見つからなければ空文字列を返します。</returns>
        internal static string GetFirstBlendShapeBindingName(GameObject avatar, BlendShapePreset preset)
        {
            var renderer = GetFirstSkinnedMeshRenderer(avatar: avatar, preset: preset);
            if (!renderer)
            {
                return "";
            }

            var clip = GetBlendShapeClip(avatar: avatar, preset: preset);
            if (!clip)
            {
                return "";
            }

            BlendShapeBinding[] bindings = clip.Values;
            if (bindings.Length == 0)
            {
                return "";
            }

            Mesh mesh = renderer.sharedMesh;
            if (mesh.blendShapeCount <= bindings[0].Index) {
                return "";
            }

            return mesh.GetBlendShapeName(shapeIndex: bindings[0].Index);
        }

        /// <summary>
        /// 指定したブレンドシェイプに対応する<see cref="BlendShapeClip">を返します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <returns></returns>
        internal static BlendShapeClip GetBlendShapeClip(GameObject avatar, BlendShapePreset preset)
        {
            var blendShapeProxy = avatar.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy)
            {
                return null;
            }

            BlendShapeAvatar blendShapeAvatar = blendShapeProxy.BlendShapeAvatar;
            if (!blendShapeAvatar)
            {
                return null;
            }

            return blendShapeAvatar.GetClip(preset: preset);
        }

        /// <summary>
        /// 指定したパスが含まれる<see cref="BlendShapeClip">のパスを置換します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="oldPath"></param>
        /// <param name="newPath"></param>
        internal static void ReplaceBlendShapeRelativePaths(GameObject avatar, string oldPath, string newPath)
        {
            var blendShapeProxy = avatar.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy)
            {
                return;
            }

            if (!blendShapeProxy.BlendShapeAvatar || blendShapeProxy.BlendShapeAvatar.Clips == null)
            {
                return;
            }

            foreach (BlendShapeClip clip in blendShapeProxy.BlendShapeAvatar.Clips) {
                if (!clip || clip.Values == null) {
                    continue;
                }

                clip.Values = clip.Values.Select(binding => {
                    string relativePath = binding.RelativePath;
                    if (relativePath == oldPath || relativePath.StartsWith(oldPath + "/"))
                    {
                        binding.RelativePath = newPath + relativePath.Substring(startIndex: oldPath.Length);
                    }
                    return binding;
                }).ToArray();
            }
        }

        /// <summary>
        /// 指定した<see cref="BlendShapeBinding"/>を置換します。
        /// </summary>
        /// <param name="blendShapeAvatar"></param>
        /// <param name="oldBinding"></param>
        /// <param name="newBinding"></param>
        internal static void ReplaceBlendShapeBinding(
            BlendShapeAvatar blendShapeAvatar,
            BlendShapeBinding oldBinding,
            BlendShapeBinding newBinding
        ) {
            foreach (BlendShapeClip clip in blendShapeAvatar.Clips)
            {
                if (!clip || clip.Values == null)
                {
                    continue;
                }

                for (var i = 0; i < clip.Values.Length; i++)
                {
                    BlendShapeBinding binding = clip.Values[i];
                    if (!binding.Equals(oldBinding))
                    {
                        continue;
                    }

                    clip.Values[i] = newBinding;
                }
            }
        }
    }
}