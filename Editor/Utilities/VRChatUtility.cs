using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRC.Core;
using VRCSDK2.Validation.Performance.Stats;
#endif

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// VRChat関連の処理など。
    /// </summary>
    internal class VRChatUtility
    {
        /// <summary>
        /// 表情の設定に利用するアニメーション名。
        /// </summary>
        internal enum Anim
        {
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP,
            FINGERPOINT,
        }

        /// <summary>
        /// アバターの最小の肩の位置。
        /// </summary>
        internal static readonly float MinShoulderHeight = 0.2f;

        /// <summary>
        /// VRChat SDKがサポートするバージョンのUnityのダウンロード先。
        /// </summary>
        internal static readonly string DownloadURL = "https://docs.vrchat.com/docs/current-unity-version";

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

#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
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
#endif

        /// <summary>
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルのGUID。
        /// </summary>
        private static readonly string CustomAnimsTemplateGUID = "4bd8fbaef3c3de041a22200917ae98b8";

        /// <summary>
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルのパス。
        /// </summary>
        private static readonly string CustomAnimsTemplatePath
            = "Assets/VRCSDK/Examples2/Animation/SDK2/CustomOverrideEmpty.overrideController";

        /// <summary>
        /// VRChat SDKがサポートするUnityのバージョンを取得します。
        /// </summary>
        /// <returns>取得できなかった場合は空文字列を返します。</returns>
        internal static string GetSupportedUnityVersion()
        {
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            return RemoteConfig.HasKey("sdkUnityVersion") ? RemoteConfig.GetString("sdkUnityVersion") : "";
#else
            return "";
#endif
        }

        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.CustomStandingAnims"/>、および<see cref="VRC_AvatarDescriptor.CustomSittingAnims"/>を作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <exception cref="FileNotFoundException">VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルが見つからなかった場合。</exception>
        /// <returns></returns>
        internal static void AddCustomAnims(GameObject avatar)
        {
#if VRC_SDK_VRCSDK2
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            var templatePath = AssetDatabase.GUIDToAssetPath(VRChatUtility.CustomAnimsTemplateGUID);
            if (string.IsNullOrEmpty(templatePath))
            {
                templatePath = VRChatUtility.CustomAnimsTemplatePath;
            }
            var template = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(templatePath);
            if (!template)
            {
                new FileNotFoundException("VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルが見つかりません。", fileName: templatePath);
            }
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
#endif
        }
    }
}
