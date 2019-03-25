using System;
using System.Linq;
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
        public static readonly string Version = "5.0.1";

        /// <summary>
        /// プレハブをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="prefabPath">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="defaultAnimationSet"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="fixVRoidSlopingShoulders">VRoid Studioから出力されたモデルがなで肩になる問題について、ボーンのPositionを変更するなら<c>true</c>。</param>
        /// <param name="changeMaterialsForWorldsNotHavingDirectionalLight">Directional Lightがないワールド向けにマテリアルを変更するなら <c>true</c>。</param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<Converter.Message> Convert(
            GameObject prefabInstance,
            VRC_AvatarDescriptor.AnimationSet defaultAnimationSet,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter = null,
            bool enableAutoEyeMovement = true,
            bool fixVRoidSlopingShoulders = true,
            bool changeMaterialsForWorldsNotHavingDirectionalLight = true
        ) {
            var messages = new List<Converter.Message>();
            messages.AddRange(GeometryCorrector.Apply(avatar: prefabInstance));
            BlendShapeReplacer.Apply(avatar: prefabInstance);
            ComponentsReplacer.Apply(avatar: prefabInstance, defaultAnimationSet: defaultAnimationSet, swayingParametersConverter: swayingParametersConverter);
            messages.AddRange(VRChatsBugsWorkaround.Apply(
                avatar: prefabInstance,
                enableAutoEyeMovement: enableAutoEyeMovement,
                fixVRoidSlopingShoulders: fixVRoidSlopingShoulders,
                changeMaterialsForWorldsNotHavingDirectionalLight: changeMaterialsForWorldsNotHavingDirectionalLight
            ));
            prefabInstance.GetOrAddComponent<PipelineManager>();
            ComponentsRemover.Apply(avatar: prefabInstance);
            Undo.RegisterCreatedObjectUndo(prefabInstance, "Convert VRM for VRChat");
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
    }
}
