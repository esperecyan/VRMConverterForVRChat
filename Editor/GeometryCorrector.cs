using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// サイズ等に関する調整。
    /// </summary>
    /// <seealso cref="VRC_SdkControlPanel.OnGUIAvatarCheck"/>
    public class GeometryCorrector
    {
        internal static IEnumerable<Converter.Message> Apply(GameObject avatar)
        {
            var messages = new List<Converter.Message>();

            Bounds bounds;
            int polycount;
            VRChatUtility.AnalyzeGeometry(go: avatar, bounds: out bounds, polycount: out polycount);

            if (polycount > VRChatUtility.MaxPolygonCount)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(
                        Gettext._("The number of polygons is {0}. If a number of polygons exceeds {1}, you can not upload."),
                        polycount,
                        VRChatUtility.MaxPolygonCount
                    ),
                    type = MessageType.Error,
                });
            }

            float scale = new[] {
                VRChatUtility.MaxSize.x / bounds.size.x,
                VRChatUtility.MaxSize.y / bounds.size.y,
                VRChatUtility.MaxSize.z / bounds.size.z,
            }.Min();
            if (scale < 1)
            {
                avatar.transform.localScale *= scale;
                messages.Add(new Converter.Message
                {
                    message = string.Format(
                        Gettext._("The avatar is scaled to {0} times to be settled in uploadable height {1} Unit, width {2} Unit, and depth {3} Unit."),
                        scale,
                        VRChatUtility.MaxSize.y,
                        VRChatUtility.MaxSize.x,
                        VRChatUtility.MaxSize.z
                    ),
                    type = MessageType.Warning,
                });
            }

            float shoulderHeight = (avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm).transform.position - avatar.transform.position).y;
            if (shoulderHeight < VRChatUtility.MinShoulderHeight)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(
                        Gettext._("The shoulders is in {0} Unit. You can not upload, if the shoulders is not in over than {1} Unit."),
                        shoulderHeight,
                        VRChatUtility.MinShoulderHeight
                    ),
                    type = MessageType.Error,
                });
            }

            return messages;
        }
    }
}
