#if VRC_SDK_VRCSDK2
using UnityEngine;
using VRCSDK2.Validation;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// <see cref="AvatarValidation.RemoveIllegalComponentsEnumerator"/>が動作しないため、その代替。
    /// </summary>
    internal class ComponentsRemover
    {
        internal static void Apply(GameObject avatar)
        {
            foreach (Component component in AvatarValidation.FindIllegalComponents(avatar)) {
                Object.DestroyImmediate(component);
            }
        }
    }
}
#endif
