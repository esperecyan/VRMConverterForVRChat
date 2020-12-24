
namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// 最新の草案における表情のプリセット。
    /// </summary>
    /// <remarks>
    /// https://github.com/vrm-c/vrm-specification/blob/a5cf0747037724d991f9ee4768ea8e07fd8b8f3f/specification/VRMC_vrm-1.0_draft/schema/VRMC_vrm.expression.schema.json
    /// </remarks>
    public enum ExpressionPreset
    {
        Aa,
        Ih,
        Ou,
        Ee,
        Oh,
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Joy"/> と同じ。
        /// </summary>
        Happy,
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Angry"/> と同じ。
        /// </summary>
        Angry,
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Sorrow"/> と同じ。
        /// </summary>
        Sad,
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Fun"/> と同じ。
        /// </summary>
        Relaxed,
        /// <summary>
        /// 驚いた。VRM-1.0において追加予定の表情。
        /// </summary>
        Surprised,
        Blink,
        BlinkLeft,
        BlinkRight,
    }
}
