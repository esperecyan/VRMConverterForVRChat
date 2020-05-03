using UnityEngine;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// L10N。
    /// </summary>
    internal class LocalizableAttribute : PropertyAttribute
    {
        internal readonly float min;
        internal readonly float max;

        internal LocalizableAttribute()
        {
        }

        internal LocalizableAttribute(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
