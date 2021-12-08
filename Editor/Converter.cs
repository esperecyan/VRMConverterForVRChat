using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
#if VRC_SDK_VRCSDK3
using VRC.Core;
using VRC.SDK3.Avatars.Components;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Components;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// アバターの変換を行うパブリックAPI。
    /// </summary>
    public class Converter
    {
        [Serializable]
        private class Package
        {
            [SerializeField]
#pragma warning disable IDE1006 // 命名スタイル
            internal string version;
#pragma warning restore IDE1006 // 命名スタイル
        }

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
        /// 変換元のアバターのルートに設定されている必要があるコンポーネント。
        /// </summary>
        public static readonly Type[] RequiredComponents = { typeof(Animator), typeof(VRMMeta), typeof(VRMHumanoidDescription), typeof(VRMFirstPerson) };

        /// <summary>
        /// 当エディタ拡張のバージョン。
        /// </summary>
        public static string Version { get; private set; }

        private static readonly string PackageJSONGUID = "e9c5b7e14151b2a40924c59da5b8aed3";

        /// <summary>
        /// プレハブをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="prefabInstance">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="clips"><see cref="VRMUtility.GetAllVRMBlendShapeClips"/>の戻り値。</param>
        /// <param name="forQuest">Quest版用アバター向けに変換するなら <c>true</c>。</param>
        /// <param name="swayingObjectsConverterSetting">揺れ物を変換するか否かの設定。<c>forQuest</c> が <c>true</c> の場合は無視されます。</param>
        /// <param name="takingOverSwayingParameters">揺れ物のパラメータを変換せずDynamic Boneのデフォルト値を利用するなら<c>false</c>。</param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="addedArmaturePositionY">VRChat上で足が沈む問題について、Hipsボーンの一つ上のオブジェクトのPositionのYに加算する値。</param>
        /// <param name="useShapeKeyNormalsAndTangents"><c>false</c> の場合、シェイプキーの法線・接線を削除します。</param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<(string message, MessageType type)> Convert(
            GameObject prefabInstance,
            IEnumerable<VRMBlendShapeClip> clips,
            bool forQuest,
            SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            bool takingOverSwayingParameters = true,
            SwayingParametersConverter swayingParametersConverter = null,
            VRMBlendShapeClip vrmBlendShapeForFINGERPOINT = null,
            float addedShouldersPositionY = 0.0f,
            float addedArmaturePositionY = 0.0f,
            bool useShapeKeyNormalsAndTangents = false
        )
        {
            AssetDatabase.SaveAssets();

#if VRC_SDK_VRCSDK3
            prefabInstance.AddComponent<VRCAvatarDescriptor>();
            prefabInstance.GetOrAddComponent<PipelineManager>();
#else
            throw new PlatformNotSupportedException("VRChat SDK3-Avatars has not been imported.");
#endif

            var messages = new List<(string, MessageType)>();
            messages.AddRange(GeometryCorrector.Apply(avatar: prefabInstance));
            BlendShapeReplacer.Apply(
                avatar: prefabInstance,
                clips: clips,
                useShapeKeyNormalsAndTangents,
                vrmBlendShapeForFINGERPOINT
            );
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
                addedShouldersPositionY: addedShouldersPositionY,
                addedArmaturePositionY: addedArmaturePositionY
            ));
            VRChatUtility.RemoveBlockedComponents(prefabInstance, forQuest);
            Undo.RegisterCreatedObjectUndo(prefabInstance, "Convert VRM for VRChat");

            AssetDatabase.SaveAssets();
            return messages;
        }

        /// <summary>
        /// 当エディタ拡張の名称。
        /// </summary>
        internal const string Name = "VRM Converter for VRChat";

        [InitializeOnLoadMethod]
        private static void LoadVersion()
        {
            var package = AssetDatabase.LoadAssetAtPath<TextAsset>(
                AssetDatabase.GUIDToAssetPath(Converter.PackageJSONGUID)
            );
            if (package == null)
            {
                return;
            }
            Converter.Version = JsonUtility.FromJson<Package>(package.text).version;
        }
    }
}
