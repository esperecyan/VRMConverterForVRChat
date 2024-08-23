#nullable enable
using System.Collections.Generic;

namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// 最新の草案における表情のプリセット。
    /// </summary>
    /// <remarks>
    /// https://github.com/vrm-c/vrm-specification/blob/a5cf0747037724d991f9ee4768ea8e07fd8b8f3f/specification/VRMC_vrm-1.0_draft/schema/VRMC_vrm.expression.schema.json
    /// </remarks>
    public class ExpressionPreset
    {
        public static readonly ExpressionPreset Aa = new ExpressionPreset("Aa");
        public static readonly ExpressionPreset Ih = new ExpressionPreset("Ih");
        public static readonly ExpressionPreset Ou = new ExpressionPreset("Ou");
        public static readonly ExpressionPreset Ee = new ExpressionPreset("Ee");
        public static readonly ExpressionPreset Oh = new ExpressionPreset("Oh");
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Joy"/> と同じ。
        /// </summary>
        public static readonly ExpressionPreset Happy = new ExpressionPreset("Happy");
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Angry"/> と同じ。
        /// </summary>
        public static readonly ExpressionPreset Angry = new ExpressionPreset("Angry");
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Sorrow"/> と同じ。
        /// </summary>
        public static readonly ExpressionPreset Sad = new ExpressionPreset("Sad");
        /// <summary>
        /// <see cref="VRM.BlendShapePreset.Fun"/> と同じ。
        /// </summary>
        public static readonly ExpressionPreset Relaxed = new ExpressionPreset("Relaxed");
        /// <summary>
        /// 驚いた。VRM-1.0において追加予定の表情。
        /// </summary>
        public static readonly ExpressionPreset Surprised = new ExpressionPreset("Surprised");
        public static readonly ExpressionPreset Blink = new ExpressionPreset("Blink");
        public static readonly ExpressionPreset BlinkLeft = new ExpressionPreset("BlinkLeft");
        public static readonly ExpressionPreset BlinkRight = new ExpressionPreset("BlinkRight");

        public string Name { get; }

        private ExpressionPreset(string name)
        {
            Name = name;
        }

        public static ExpressionPreset CreateCustom(string name)
        {
            return new ExpressionPreset(name);
        }

        public static IEnumerable<ExpressionPreset> GetAllPresets()
        {
            yield return Aa;
            yield return Ih;
            yield return Ou;
            yield return Ee;
            yield return Oh;
            yield return Happy;
            yield return Angry;
            yield return Sad;
            yield return Relaxed;
            yield return Surprised;
            yield return Blink;
            yield return BlinkLeft;
            yield return BlinkRight;
        }
    }
}