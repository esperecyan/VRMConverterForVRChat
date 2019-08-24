using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.Core;
using VRCSDK2;
using VRCSDK2.Validation.Performance.Stats;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRChat関連の処理など。
    /// </summary>
    public class VRChatUtility
    {
        /// <summary>
        /// 表情の設定に利用するアニメーション名。
        /// </summary>
        public enum Anim
        {
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP,
        }
        
        /// <summary>
        /// アバターの最小の肩の位置。
        /// </summary>
        public static readonly float MinShoulderHeight = 0.2f;

        /// <summary>
        /// VRChat SDKがサポートするバージョンのUnityのダウンロード先。
        /// </summary>
        public static readonly string DownloadURL = "https://api.vrchat.cloud/home/download";

        /// <summary>
        /// オートアイムーブメントの有効化に必要となるダミーの階層構造。
        /// </summary>
        internal static readonly string[] RequiredPathForAutoEyeMovement = new[] {
            "Armature/Hips/Spine/Chest/Neck/Head/LeftEye",
            "Armature/Hips/Spine/Chest/Neck/Head/RightEye",
        };

        /// <summary>
        /// 自動まばたきに利用されるメッシュのオブジェクトのパス。
        /// </summary>
        internal static readonly string AutoBlinkMeshPath = "Body";

        /// <summary>
        /// アニメーションオーバーライドの有効化に必須となるボーン。
        /// </summary>
        internal static readonly HumanBodyBones[][] RequiredHumanBodyBonesForAnimationOverride = new HumanBodyBones[][] {
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftHand, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightHand, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        /// <summary>
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルのパス。
        /// </summary>
        internal static readonly string CustomAnimsTemplatePath = "Assets/VRCSDK/Examples/Sample Assets/Animation/CustomOverrideEmpty.overrideController";

        /// <summary>
        /// プラットフォームごとの<see cref="AvatarPerformanceStatsLevelSet"/>。
        /// </summary>
        internal static IDictionary<string, AvatarPerformanceStatsLevelSet> AvatarPerformanceStatsLevelSets
            = new Dictionary<string, AvatarPerformanceStatsLevelSet>() {
                { "PC", Resources.Load<AvatarPerformanceStatsLevelSet>(
                    "Validation/Performance/StatsLevels/Windows/AvatarPerformanceStatLevels_Windows"
                ) },
                { "Quest", Resources.Load<AvatarPerformanceStatsLevelSet>(
                    "Validation/Performance/StatsLevels/Quest/AvatarPerformanceStatLevels_Quest"
                ) },
            };

        /// <summary>
        /// VRChat SDKがサポートするUnityのバージョンを取得します。
        /// </summary>
        /// <returns>取得できなかった場合は空文字列を返します。</returns>
        internal static string GetSupportedUnityVersion()
        {
            return RemoteConfig.HasKey("sdkUnityVersion") ? RemoteConfig.GetString("sdkUnityVersion") : "";
        }

        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.CustomStandingAnims"/>、および<see cref="VRC_AvatarDescriptor.CustomSittingAnims"/>を作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        internal static void AddCustomAnims(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            var template = AssetDatabase.LoadMainAssetAtPath(VRChatUtility.CustomAnimsTemplatePath)
                as AnimatorOverrideController;

            if (!avatarDescriptor.CustomStandingAnims)
            {
                avatarDescriptor.CustomStandingAnims = Duplicator.DuplicateAssetToFolder<AnimatorOverrideController>(
                    source: template,
                    prefabInstance: avatar,
                    fileName: "CustomStandingAnims.overrideController"
                );
            }

            if (!avatarDescriptor.CustomSittingAnims)
            {
                avatarDescriptor.CustomSittingAnims = Duplicator.DuplicateAssetToFolder<AnimatorOverrideController>(
                    source: template,
                    prefabInstance: avatar,
                    fileName: "CustomSittingAnims.overrideController"
                );
            }
        }
    }
}
