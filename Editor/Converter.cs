using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using VRM;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using Esperecyan.UniVRMExtensions.SwayingObjects;
using Esperecyan.Unity.VRMConverterForVRChat.Components;
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
        /// OSCの受信対象。
        /// </summary>
        [Flags]
        public enum OSCComponents
        {
            None = 0,
            Blink = 1 << 0,
        }

        /// <summary>
        /// 変換元のアバターのルートに設定されている必要があるコンポーネント。
        /// </summary>
        public static readonly Type[] RequiredComponents = { typeof(Animator), typeof(VRMMeta), typeof(VRMHumanoidDescription), typeof(VRMFirstPerson) };

        /// <summary>
        /// 変換後の処理を行うコールバック関数。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="meta"></param>
        public delegate void PostConverting(GameObject avatar, VRMMeta meta);

        /// <summary>
        /// プレハブをVRChatへアップロード可能な状態にします。
        /// </summary>
        /// <param name="prefabInstance">現在のシーンに存在するプレハブのインスタンス。</param>
        /// <param name="clips"><see cref="VRMUtility.GetAllVRMBlendShapeClips"/>の戻り値。</param>
        /// <param name="forQuest">Quest版用アバター向けに変換するなら <c>true</c>。</param>
        /// <param name="swayingObjectsConverterSetting">揺れ物を変換するか否かの設定。<c>forQuest</c> が <c>true</c> の場合は無視されます。</param>
        /// <param name="takingOverSwayingParameters">揺れ物のパラメータを変換せずDynamic Boneのデフォルト値を利用するなら<c>false</c>。</param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="addedArmaturePositionY">VRChat上で足が沈む問題について、Hipsボーンの一つ上のオブジェクトのPositionのYに加算する値。</param>
        /// <param name="useShapeKeyNormalsAndTangents"><c>false</c> の場合、シェイプキーの法線・接線を削除します。</param>
        /// <param name="oscComponents"></param>
        /// <param name="postConverting"></param>
        /// <returns>変換中に発生したメッセージ。</returns>
        public static IEnumerable<(string message, MessageType type)> Convert(
            GameObject prefabInstance,
            IEnumerable<VRMBlendShapeClip> clips,
            bool forQuest,
            SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            bool takingOverSwayingParameters = true,
            VRMSpringBonesToVRCPhysBonesConverter.ParametersConverter swayingParametersConverter = null,
            VRMBlendShapeClip vrmBlendShapeForFINGERPOINT = null,
            bool keepingUpperChest = false,
            float addedShouldersPositionY = 0.0f,
            float addedArmaturePositionY = 0.0f,
            bool useShapeKeyNormalsAndTangents = false,
            OSCComponents oscComponents = OSCComponents.Blink,
            PostConverting postConverting = null
        )
        {
            AssetDatabase.SaveAssets();

            prefabInstance.AddComponent<VRCAvatarDescriptor>();
            prefabInstance.GetOrAddComponent<PipelineManager>();

            var messages = new List<(string, MessageType)>();
            messages.AddRange(GeometryCorrector.Apply(avatar: prefabInstance));
            BlendShapeReplacer.Apply(
                avatar: prefabInstance,
                clips: clips,
                useShapeKeyNormalsAndTangents,
                vrmBlendShapeForFINGERPOINT,
                oscComponents
            );
            messages.AddRange(ComponentsReplacer.Apply(
                avatar: prefabInstance,
                swayingObjectsConverterSetting: swayingObjectsConverterSetting,
                swayingParametersConverter: takingOverSwayingParameters
                    ? swayingParametersConverter
                    : null,
                forQuest
            ));
            messages.AddRange(VRChatsBugsWorkaround.Apply(
                avatar: prefabInstance,
                keepingUpperChest,
                addedShouldersPositionY: addedShouldersPositionY,
                addedArmaturePositionY: addedArmaturePositionY
            ));

            var meta = prefabInstance.GetComponent<VRMMeta>();
            VRChatUtility.RemoveBlockedComponents(prefabInstance, forQuest);
            Undo.RegisterCreatedObjectUndo(prefabInstance, "Convert VRM for VRChat");

            AssetDatabase.SaveAssets();

            postConverting?.Invoke(prefabInstance, meta);

            if (forQuest)
            {
                messages.AddRange(VRChatUtility.CalculateQuestVRCPhysBoneLimitations(prefabInstance));
            }

            return messages;
        }

        /// <summary>
        /// 当エディタ拡張のバージョンを取得します。
        /// </summary>
        /// <returns></returns>
        public static Task<string> GetVersion()
        {
            var request = Client.List(offlineMode: true, includeIndirectDependencies: true);
            var taskCompleteSource = new TaskCompletionSource<string>();
            void Handler()
            {
                if (!request.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= Handler;

                taskCompleteSource.SetResult(
                    request.Result.FirstOrDefault(info => info.name == "jp.pokemori.vrm-converter-for-vrchat")?.version
                );
            }

            EditorApplication.update += Handler;

            return taskCompleteSource.Task;
        }

        /// <summary>
        /// 当エディタ拡張の名称。
        /// </summary>
        internal const string Name = "VRM Converter for VRChat";
    }
}
