using System.Collections.Generic;
using UnityEngine;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRoid Studio関連の処理など。
    /// </summary>
    /// <remarks>
    /// 参照:
    /// VRoid studioで作ったモデルをVRChatにアップロードする際の注意点 — yupaがエンジニアになるまでを記録するブログ
    /// <https://yu8as.hatenablog.com/entry/2018/08/25/004856>
    /// 猫田あゆむ🐈VTuber｜仮想秘密結社「ネコミミナティ」さんのツイート: “何度もすみません。FBXのRigからBone座標を設定する場合は、ShoulderのY座標をチョイあげ（0.12...くらい）、Upper ArmのY座標を0にするといい感じになるそうです。もしかしたらコレVRoidのモデル特有の話かもしれないのですが・・・。… https://t.co/d7Jw7qoXBX”
    /// https://twitter.com/virtual_ayumu/status/1051146511197790208
    /// </remarks>
    internal class VRoidUtility
    {
        /// <summary>
        /// VRChat上でなで肩になる問題を解消するために加算するPosition値。
        /// </summary>
        internal static readonly Vector3 AddedPositionValueForVRChat = new Vector3(0, 0.02f, 0);

        /// <summary>
        /// VRChat上でなで肩になる問題を解消するために変更する必要があるボーンとオブジェクト名。
        /// </summary>
        internal static readonly IDictionary<HumanBodyBones, string> RequiredModifiedBonesAndNamesForVRChat = new Dictionary<HumanBodyBones, string> {
            { HumanBodyBones.LeftShoulder, "J_Bip_L_Shoulder" },
            { HumanBodyBones.RightShoulder, "J_Bip_R_Shoulder" },
            { HumanBodyBones.LeftUpperArm, "J_Bip_L_UpperArm" },
            { HumanBodyBones.RightUpperArm, "J_Bip_R_UpperArm" },
        };
    }
}