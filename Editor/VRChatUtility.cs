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
        internal static readonly IDictionary<HumanBodyBones, HumanBodyBones[]>
			RequiredHumanBodyBonesForAnimationOverride = new Dictionary<HumanBodyBones, HumanBodyBones[]> {
				{ HumanBodyBones.LeftHand, new[] {
					HumanBodyBones.LeftThumbProximal,
					HumanBodyBones.LeftIndexProximal,
					HumanBodyBones.LeftMiddleProximal,
				} },
				{ HumanBodyBones.RightHand, new[] {
					HumanBodyBones.RightThumbProximal,
					HumanBodyBones.RightIndexProximal,
					HumanBodyBones.RightMiddleProximal,
				} },
			};

        /// <summary>
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルのパス。
        /// </summary>
        internal static readonly string CustomAnimsTemplatePath = "Assets/VRCSDK/Examples2/Animation/SDK2/CustomOverrideEmpty.overrideController";

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
            var template
                = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(VRChatUtility.CustomAnimsTemplatePath);

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
