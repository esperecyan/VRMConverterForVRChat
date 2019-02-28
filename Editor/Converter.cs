using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using VRCSDK2;
using VRC.Core;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// アバターの変換を行うパブリックAPI。
    /// </summary>
    public class Converter
    {
        /// <summary>
        /// <see cref="EditorGUILayout.HelpBox"/>で表示するメッセージ。
        /// </summary>
        public struct Message
        {
            public string message;
            public MessageType type;
        }

        /// <summary>
        /// 変換元のアバターのルートに設定されている必要があるコンポーネント。
        /// </summary>
        public static readonly Type[] RequiredComponents = { typeof(Animator), typeof(VRMMeta), typeof(VRMHumanoidDescription), typeof(VRMFirstPerson) };

        /// <summary>
        /// 当エディタ拡張のバージョン。
        /// </summary>
        public static readonly string Version = "3.0.0";

        /// <summary>
        /// Hierarchy上のアバターをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="avatar"><see cref="Converter.RequiredComponents"/>が設定されたインスタンス。</param>
        /// <param name="defaultAnimationSet"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="assetsPath">「Assets/」から始まるVRMプレハブのパス。</param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="fixVRoidSlopingShoulders">VRoid Studioから出力されたモデルがなで肩になる問題について、ボーンのPositionを変更するなら<c>true</c>。</param>
        /// <param name="changeMaterialsForWorldsNotHavingDirectionalLight">Directional Lightがないワールド向けにマテリアルを変更するなら <c>true</c>。</param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<Converter.Message> Convert(
            GameObject avatar,
            VRC_AvatarDescriptor.AnimationSet defaultAnimationSet,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter = null,
            string assetsPath = "",
            bool enableAutoEyeMovement = true,
            bool fixVRoidSlopingShoulders = true,
            bool changeMaterialsForWorldsNotHavingDirectionalLight = true
        ) {
#pragma warning disable 618
            avatar.SetActiveRecursively(state: true); // GameObject.setActive() は子孫の有効・無効を切り替えない
#pragma warning restore 618
            IEnumerable<Converter.Message> messages = GeometryCorrector.Apply(avatar: avatar);
            BlendShapeReplacer.Apply(avatar: avatar, assetsPath: assetsPath);
            ComponentsReplacer.Apply(avatar: avatar, defaultAnimationSet: defaultAnimationSet, swayingParametersConverter: swayingParametersConverter);
            VRChatsBugsWorkaround.Apply(
                avatar: avatar,
                assetsPath: assetsPath,
                enableAutoEyeMovement: enableAutoEyeMovement,
                fixVRoidSlopingShoulders: fixVRoidSlopingShoulders,
                changeMaterialsForWorldsNotHavingDirectionalLight: changeMaterialsForWorldsNotHavingDirectionalLight
            );
            avatar.GetOrAddComponent<PipelineManager>();
            ComponentsRemover.Apply(avatar: avatar);
            Undo.RegisterCreatedObjectUndo(avatar, "Convert VRM for VRChat");
            return messages;
        }

        /// <summary>
        /// 当エディタ拡張の名称。
        /// </summary>
        internal const string Name = "VRM Converter for VRChat";

        /// <summary>
        /// 当エディタ拡張が保存されているフォルダのパス。
        /// </summary>
        internal static readonly string RootFolderPath = "Assets/VRMConverterForVRChat";

        /// <summary>
        /// 変換後のアバター固有のファイルを保存するフォルダパスを取得します。
        /// </summary>
        /// <remarks>
        /// フォルダが存在しない場合は作成します。
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="assetsPath"></param>
        /// <returns></returns>
        internal static string GetAnimationsFolderPath(GameObject avatar, string assetsPath)
        {
            UnityPath path = UnityPath.FromUnityPath(string.IsNullOrEmpty(assetsPath) ? "Assets/" + avatar.name : assetsPath).GetAssetFolder(".Animations");
            path.EnsureFolder();
            return path.Value;
        }
    }
}
