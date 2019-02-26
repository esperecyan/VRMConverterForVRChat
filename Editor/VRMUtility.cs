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
        /// <param name="leadingPath"></param>
        /// <returns></returns>
        internal static SkinnedMeshRenderer GetFirstSkinnedMeshRenderer(GameObject avatar, BlendShapePreset preset, string leadingPath = "")
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
            Transform face = avatar.transform.Find(name: binding.RelativePath)
                ?? (leadingPath != "" ? avatar.transform.Find(name: leadingPath + binding.RelativePath) : null);
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
        /// <param name="leadingPath"></param>
        /// <returns>対応するブレンドシェイプ名が見つからなければ空文字列を返します。</returns>
        internal static string GetFirstBlendShapeBindingName(GameObject avatar, BlendShapePreset preset, string leadingPath = "")
        {
            var renderer = GetFirstSkinnedMeshRenderer(avatar: avatar, preset: preset, leadingPath: leadingPath);
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
    }
}