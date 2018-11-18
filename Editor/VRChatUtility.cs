using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;

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
        /// アバターの最大ポリゴン数。
        /// </summary>
        public static readonly int MaxPolygonCount = 19999;

        /// <summary>
        /// アバターの最大サイズ。
        /// </summary>
        public static readonly Vector3 MaxSize = new Vector3 { x = 5.0f, y = 6.0f, z = 5.0f };

        /// <summary>
        /// アバターの最小の方の位置。
        /// </summary>
        public static readonly float MinShoulderHeight = 0.2f;

        /// <summary>
        /// VRChat SDKがサポートするUnityのバージョン。
        /// </summary>
        public static readonly string SupportedUnityVersion = "5.6.3p1";

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
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルを複製します。
        /// </summary>
        /// <param name="path">複製したファイルの保存先。</param>
        /// <returns></returns>
        internal static AnimatorOverrideController CreateCustomStandingAnims(string path)
        {
            AssetDatabase.CopyAsset(
                path: "Assets/VRCSDK/Examples/Sample Assets/Animation/CustomOverrideEmpty.overrideController",
                newPath: path
            );

            return AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath: path);
        }

        /// <summary>
        /// カスタムアニメーション用のアニメーションクリップのテンプレートを複製します。
        /// </summary>
        /// <param name="name"></param>
        /// <param name="path">複製したファイルの保存先。</param>
        /// <returns></returns>
        internal static AnimationClip CreateAnim(Anim name, string path)
        {
            AssetDatabase.CopyAsset(
                path: Path.Combine(Path.Combine(Converter.RootFolderPath, "Editor"), name + ".anim"),
                newPath: path
            );

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath: path);
        }

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
    }
}
