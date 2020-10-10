using UnityEngine;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// L10N。
    /// </summary>
    internal class LocalizableAttribute : PropertyAttribute
    {
        internal readonly float Min;
        internal readonly float Max;

        internal LocalizableAttribute()
        {
        }

        internal LocalizableAttribute(float min, float max)
        {
            this.Min = min;
            this.Max = max;
        }
    }
}
