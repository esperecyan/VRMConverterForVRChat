using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// サイズ等に関する調整。
    /// </summary>
    /// <seealso cref="VRC_SdkControlPanel.OnGUIAvatarCheck"/>
    internal class GeometryCorrector
    {
        internal static IEnumerable<(string, MessageType)> Apply(GameObject avatar)
        {
            var messages = new List<(string, MessageType)>();

            var shoulderHeight = (avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm).transform.position - avatar.transform.position).y;
            if (shoulderHeight <= 0)
            {
                messages.Add((string.Format(
                    _("The shoulders is in {0} Unit. You can not upload, if the shoulders is not in over than {1} Unit."),
                    shoulderHeight,
                    VRChatUtility.MinShoulderHeight
                ), MessageType.Error));
            }
            else if (shoulderHeight < VRChatUtility.MinShoulderHeight)
            {
                var scale = VRChatUtility.MinShoulderHeight / shoulderHeight;
                avatar.transform.localScale *= scale;
                messages.Add((string.Format(
                    _("The avatar is scaled to {0} times to be settled in uploadable shoulders height {1} Unit."),
                    scale,
                    VRChatUtility.MinShoulderHeight
                ), MessageType.Warning));
            }

            return messages;
        }
    }
}
