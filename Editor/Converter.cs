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
        public static readonly string Version = "7.1.1";

        /// <summary>
        /// プレハブをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="prefabPath">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="swayingObjectsConverterSetting">揺れ物を変換するか否かの設定。</param>
        /// <param name="takingOverSwayingParameters">揺れ物のパラメータを変換せずDynamic Boneのデフォルト値を利用するなら<c>false</c>。</param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="changeMaterialsForWorldsNotHavingDirectionalLight">Directional Lightがないワールド向けにマテリアルを変更するなら <c>true</c>。</param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<Converter.Message> Convert(
            GameObject prefabInstance,
            ComponentsReplacer.SwayingObjectsConverterSetting swayingObjectsConverterSetting
                = default(ComponentsReplacer.SwayingObjectsConverterSetting),
            bool takingOverSwayingParameters = true,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter = null,
            bool enableAutoEyeMovement = true,
            float addedShouldersPositionY = 0.0f,
            bool changeMaterialsForWorldsNotHavingDirectionalLight = true
        ) {
            var messages = new List<Converter.Message>();
            messages.AddRange(GeometryCorrector.Apply(avatar: prefabInstance));
            BlendShapeReplacer.Apply(avatar: prefabInstance);
            messages.AddRange(ComponentsReplacer.Apply(
                avatar: prefabInstance,
                swayingObjectsConverterSetting: swayingObjectsConverterSetting,
                swayingParametersConverter: takingOverSwayingParameters
                    ? swayingParametersConverter ?? ComponentsReplacer.DefaultSwayingParametersConverter
                    : null
            ));
            messages.AddRange(VRChatsBugsWorkaround.Apply(
                avatar: prefabInstance,
                enableAutoEyeMovement: enableAutoEyeMovement,
                addedShouldersPositionY: addedShouldersPositionY,
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
