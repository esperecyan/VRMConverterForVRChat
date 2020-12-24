
using System.Collections.Generic;
using UnityEngine;

namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// <see cref="ExpressionPreset"/>と対応するVRChatアバターの表情。
    /// <see cref="AnimationClip"/>、<see cref="ShapeKeyNames"/>はどちらか一方のみ指定。
    /// </summary>
    public struct VRChatExpressionBinding
    {
        /// <summary>
        /// <see cref="AnimationClip"/>が設定されているGameObject、
        /// または<see cref="ShapeKeyNames"/>を含むメッシュが設定されているGameObjectのルートからのパス。
        /// </summary>
        public string RelativePath;

        /// <summary>
        /// 1フレーム目に表情のキーを含むアニメーションクリップ。
        /// </summary>
        public AnimationClip AnimationClip;

        /// <summary>
        /// 表情のシェイプキー名。
        /// </summary>
        public IEnumerable<string> ShapeKeyNames;
    }
}
