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
                    message = string.Format("ポリゴン数が{0}です。ポリゴン数が{1}を超える場合、アップロードできません。", polycount, VRChatUtility.MaxPolygonCount),
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
                        "アバターを{0}倍に縮小し、アップロード可能な高さ{1}Unit、幅{2}Unit、奥行き{3}Unitに収まるようにしました。",
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
                    message = string.Format("肩が {0} Unit の位置にあります。{1} Unit 以上でなければアップロードできません。", shoulderHeight, VRChatUtility.MinShoulderHeight),
                    type = MessageType.Error,
                });
            }

            return messages;
        }
    }
}
