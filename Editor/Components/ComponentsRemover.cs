using UnityEngine;
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRCSDK2.Validation;
#endif

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// <see cref="AvatarValidation.RemoveIllegalComponentsEnumerator"/>が動作しないため、その代替。
    /// </summary>
    internal class ComponentsRemover
    {
        internal static void Apply(GameObject avatar)
        {
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            foreach (Component component in AvatarValidation.FindIllegalComponents(avatar)) {
                Object.DestroyImmediate(component);
            }
#endif
        }
    }
}
