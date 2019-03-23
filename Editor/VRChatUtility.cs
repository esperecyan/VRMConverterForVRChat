using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using VRC.Core;

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
        internal static readonly string CustomStandingAnimsPath = "Assets/VRCSDK/Examples/Sample Assets/Animation/CustomOverrideEmpty.overrideController";

        /// <summary>
        /// <see cref="VRC_SdkControlPanel.AnalyzeGeometry"/>を実行します。
        /// </summary>
        /// <param name="go">対象のアバター。</param>
        /// <param name="bounds">大きさ。</param>
        /// <param name="polycount">ポリゴン数。</param>
        internal static void AnalyzeGeometry(GameObject go, out Bounds bounds, out int polycount)
        {
            var args = new object[] { go, null, null };
            typeof(VRC_SdkControlPanel).InvokeMember(
                name: "AnalyzeGeometry",
                invokeAttr: BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod,
                binder: null,
                target: ScriptableObject.CreateInstance<VRC_SdkControlPanel>(),
                args: args
            );

            bounds = (Bounds)args[1];
            polycount = (int)args[2];
        }

        /// <summary>
        /// VRChat SDKがサポートするUnityのバージョンを取得します。
        /// </summary>
        /// <returns>取得できなかった場合は空文字列を返します。</returns>
        internal static string GetSupportedUnityVersion()
        {
            return RemoteConfig.HasKey("sdkUnityVersion") ? RemoteConfig.GetString("sdkUnityVersion") : "";
        }
    }
}
