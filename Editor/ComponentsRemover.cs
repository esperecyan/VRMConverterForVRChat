using UnityEngine;
using VRCSDK2.Validation;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// <see cref="AvatarValidation.RemoveIllegalComponentsEnumerator"/>が動作しないため、その代替。
    /// </summary>
    internal class ComponentsRemover
    {
        internal static void Apply(GameObject avatar)
        {
            foreach (Component component in AvatarValidation.FindIllegalComponents(target: avatar)) {
                Object.DestroyImmediate(obj: component);
            }
        }
    }
}