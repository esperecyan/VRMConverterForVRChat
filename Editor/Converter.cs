using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
#if VRC_SDK_VRCSDK2
using VRC.Core;
using Esperecyan.Unity.VRMConverterForVRChat.Components;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// アバターの変換を行うパブリックAPI。
    /// </summary>
    public class Converter
    {
        /// <summary>
        /// 揺れ物を変換するか否かの設定。
        /// </summary>
        public enum SwayingObjectsConverterSetting
        {
            ConvertVrmSpringBonesOnly,
            ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups,
            RemoveSwayingObjects,
        }

        /// <summary>
        /// 揺れ物のパラメータ変換アルゴリズムの定義を行うコールバック関数。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public delegate DynamicBoneParameters SwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo);

        /// <summary>
        /// <see cref="ComponentsReplacer.SwayingParametersConverter">の既定値。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public static DynamicBoneParameters DefaultSwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo)
        {
            return new DynamicBoneParameters()
            {
                Elasticity = springBoneParameters.StiffnessForce * 0.05f,
                Damping = springBoneParameters.DragForce * 0.6f,
                Stiffness = 0,
                Inert = 0,
            };
        }

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
        public static readonly string Version = "20.0.0";

        /// <summary>
        /// プレハブをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="prefabPath">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="clips"><see cref="VRMUtility.GetAllVRMBlendShapeClips"/>の戻り値。</param>
        /// <param name="swayingObjectsConverterSetting">揺れ物を変換するか否かの設定。<c>forQuest</c> が <c>true</c> の場合は無視されます。</param>
        /// <param name="takingOverSwayingParameters">揺れ物のパラメータを変換せずDynamic Boneのデフォルト値を利用するなら<c>false</c>。</param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="fixProneAvatarPosition">伏せたときのアバターの位置が、自分視点と他者視点で異なるVRChatのバグに対処するなら <c>true</c>。</param>
        /// <param name="moveEyeBoneToFrontForEyeMovement">オートアイムーブメント有効化時、目ボーンのPositionのZに加算する値。</param>
        /// <param name="forQuest">Quest版用アバター向けに変換するなら <c>true</c>。</param>
        /// <param name="addedArmaturePositionY">VRChat上で足が沈む問題について、Hipsボーンの一つ上のオブジェクトのPositionのYに加算する値。</param>
        /// <param name="useAnimatorForBlinks">まばたきにAnimatorコンポーネントを利用するなら <c>true</c>。
        ///     <c>forQuest</c> が <c>false</c> の場合、常にAnimatorコンポーネントが利用され、このパラメータは無視されます。</param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<Converter.Message> Convert(
            GameObject prefabInstance,
            IEnumerable<VRMBlendShapeClip> clips,
            SwayingObjectsConverterSetting swayingObjectsConverterSetting = default,
            bool takingOverSwayingParameters = true,
            SwayingParametersConverter swayingParametersConverter = null,
            bool enableAutoEyeMovement = true,
            float addedShouldersPositionY = 0.0f,
            bool fixProneAvatarPosition = true,
            float moveEyeBoneToFrontForEyeMovement = 0.0f,
            bool forQuest = false,
            float addedArmaturePositionY = 0.0f,
            bool useAnimatorForBlinks = true
        ) {
#if VRC_SDK_VRCSDK2
            var messages = new List<Converter.Message>();
            messages.AddRange(GeometryCorrector.Apply(avatar: prefabInstance));
            messages.AddRange(BlendShapeReplacer.Apply(
                avatar: prefabInstance,
                clips: clips,
                useAnimatorForBlinks: forQuest ? useAnimatorForBlinks : true
            ));
            messages.AddRange(ComponentsReplacer.Apply(
                avatar: prefabInstance,
                swayingObjectsConverterSetting: forQuest
                    ? SwayingObjectsConverterSetting.RemoveSwayingObjects
                    : swayingObjectsConverterSetting,
                swayingParametersConverter: takingOverSwayingParameters
                    ? swayingParametersConverter ?? DefaultSwayingParametersConverter
                    : null
            ));
            messages.AddRange(VRChatsBugsWorkaround.Apply(
                avatar: prefabInstance,
                enableAutoEyeMovement: enableAutoEyeMovement,
                addedShouldersPositionY: addedShouldersPositionY,
                addedArmaturePositionY: addedArmaturePositionY,
                fixProneAvatarPosition: fixProneAvatarPosition,
                moveEyeBoneToFrontForEyeMovement: moveEyeBoneToFrontForEyeMovement,
                forQuest: forQuest
            ));
            prefabInstance.GetOrAddComponent<PipelineManager>();
            ComponentsRemover.Apply(avatar: prefabInstance);
            Undo.RegisterCreatedObjectUndo(prefabInstance, "Convert VRM for VRChat");
            return messages;
#else
            throw new PlatformNotSupportedException("VRCHAT SDK2 (VRCSDK2) has not been imported.");
#endif
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
