using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniGLTF;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRCSDK2.Validation.Performance;
using VRCSDK2.Validation.Performance.Stats;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// キャラクター情報、視点、揺れ物に関する設定。
    /// </summary>
    internal class ComponentsReplacer
    {
        private static readonly Type DynamicBoneType = Type.GetType("DynamicBone, Assembly-CSharp");
        private static readonly Type DynamicBoneColliderType = Type.GetType("DynamicBoneCollider, Assembly-CSharp");
        private static readonly Type DynamicBoneColliderBaseListType
            = Type.GetType("System.Collections.Generic.List`1[[DynamicBoneColliderBase, Assembly-CSharp]]");

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <returns></returns>
        internal static IEnumerable<Converter.Message> Apply(
            GameObject avatar,
            Converter.SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            Converter.SwayingParametersConverter swayingParametersConverter
        )
        {
            var messages = new List<Converter.Message>();

            ConvertMeta(avatar: avatar);
            ConvertVRMFirstPerson(avatar: avatar);

            if (DynamicBoneType == null
                || swayingObjectsConverterSetting == Converter.SwayingObjectsConverterSetting.RemoveSwayingObjects)
            {
                return messages;
            }

            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<MonoBehaviour>> dynamicBoneColliderGroups = null;
            if (swayingObjectsConverterSetting == Converter.SwayingObjectsConverterSetting.ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups)
            {
                RemoveUnusedColliderGroups(avatar: avatar);
                dynamicBoneColliderGroups = ConvertVRMSpringBoneColliderGroups(avatar);
            }
            ConvertVRMSpringBones(
                avatar: avatar,
                dynamicBoneColliderGroups: dynamicBoneColliderGroups,
                swayingParametersConverter: swayingParametersConverter
            );

            messages.AddRange(GetMessagesAboutDynamicBoneLimits(avatar: avatar));

            return messages;
        }

        /// <summary>
        /// キャラクターに関する情報を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertMeta(GameObject avatar)
        {
#if VRC_SDK_VRCSDK2
            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.Animations = VRChatsBugsWorkaround.DefaultAnimationSetValue;
#endif
        }

        /// <summary>
        /// <see cref="VRMFirstPerson"/>を基に<see cref="VRC_AvatarDescriptor"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertVRMFirstPerson(GameObject avatar)
        {
#if VRC_SDK_VRCSDK2
            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
#elif VRC_SDK_VRCSDK3
            var avatarDescriptor = avatar.GetComponent<VRCAvatarDescriptor>();
#endif
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            var firstPerson = avatar.GetComponent<VRMFirstPerson>();
            avatarDescriptor.ViewPosition = firstPerson.FirstPersonBone.position + firstPerson.FirstPersonOffset - avatar.transform.localPosition;
#endif
        }

        /// <summary>
        /// <see cref="VRMSpringBone.ColliderGroups"/>から参照されていない<see cref="VRMSpringBoneColliderGroup"/>を削除します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void RemoveUnusedColliderGroups(GameObject avatar)
        {
            IEnumerable<GameObject> objectsHavingUsedColliderGroup = avatar.GetComponentsInChildren<VRMSpringBone>()
                .SelectMany(springBone => springBone.ColliderGroups)
                .Select(colliderGroup => colliderGroup.gameObject)
                .ToArray();

            foreach (var colliderGroup in avatar.GetComponentsInChildren<VRMSpringBoneColliderGroup>())
            {
                if (!objectsHavingUsedColliderGroup.Contains(colliderGroup.gameObject))
                {
                    UnityEngine.Object.DestroyImmediate(colliderGroup);
                }
            }
        }

        /// <summary>
        /// 子孫の<see cref="VRMSpringBoneColliderGroup"/>を基に<see cref="DynamicBoneCollider"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns>キーに<see cref="VRMSpringBoneColliderGroup"/>、値に対応する<see cref="DynamicBoneCollider"/>のリストを持つジャグ配列。</returns>
        private static IDictionary<VRMSpringBoneColliderGroup, IEnumerable<MonoBehaviour>> ConvertVRMSpringBoneColliderGroups(GameObject avatar)
        {
            return avatar.GetComponentsInChildren<VRMSpringBoneColliderGroup>().ToDictionary(
                keySelector: colliderGroup => colliderGroup,
                elementSelector: colliderGroup => ConvertVRMSpringBoneColliderGroup(colliderGroup: colliderGroup)
            );
        }

        /// <summary>
        /// 指定された<see cref="VRMSpringBoneColliderGroup"/>を基に<see cref="DynamicBoneCollider"/>を設定します。
        /// </summary>
        /// <param name="colliderGroup"></param>
        /// <param name="bonesForCollisionWithOtherAvatar"></param>
        /// <returns>生成した<see cref="DynamicBoneCollider"/>のリスト。</returns>
        private static IEnumerable<MonoBehaviour> ConvertVRMSpringBoneColliderGroup(
            VRMSpringBoneColliderGroup colliderGroup
        )
        {
            var bone = colliderGroup.gameObject;

            return colliderGroup.Colliders.Select(collider => {
                var dynamicBoneCollider = bone.AddComponent(DynamicBoneColliderType);
                DynamicBoneColliderType.GetField("m_Center").SetValue(dynamicBoneCollider, collider.Offset);
                DynamicBoneColliderType.GetField("m_Radius").SetValue(dynamicBoneCollider, collider.Radius);
                return dynamicBoneCollider as MonoBehaviour;
            }).ToList();
        }

        /// <summary>
        /// 子孫の<see cref="VRMSpringBone"/>を基に<see cref="DynamicBone"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="dynamicBoneColliderGroups">キーに<see cref="VRMSpringBoneColliderGroup"/>、値に対応する<see cref="DynamicBoneCollider"/>のリストを持つジャグ配列。</param>
        /// <param name="swayingParametersConverter"></param>
        private static void ConvertVRMSpringBones(
            GameObject avatar,
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<MonoBehaviour>> dynamicBoneColliderGroups,
            Converter.SwayingParametersConverter swayingParametersConverter
        )
        {
            foreach (var springBone in avatar.GetComponentsInChildren<VRMSpringBone>())
            {
                ConvertVRMSpringBone(springBone: springBone, dynamicBoneColliderGroups: dynamicBoneColliderGroups, swayingParametersConverter: swayingParametersConverter);
            }
        }

        /// <summary>
        /// 指定された<see cref="VRMSpringBone"/>を基に<see cref="DynamicBone"/>を設定します。
        /// </summary>
        /// <param name="springBone"></param>
        /// <param name="dynamicBoneColliderGroups">キーに<see cref="VRMSpringBoneColliderGroup"/>、値に対応する<see cref="DynamicBoneCollider"/>のリストを持つジャグ配列。</param>
        /// <param name="swayingParametersConverter"></param>
        private static void ConvertVRMSpringBone(
            VRMSpringBone springBone,
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<MonoBehaviour>> dynamicBoneColliderGroups,
            Converter.SwayingParametersConverter swayingParametersConverter
        )
        {
            var springBoneParameters = new SpringBoneParameters(stiffnessForce: springBone.m_stiffnessForce, dragForce: springBone.m_dragForce);
            var boneInfo = new BoneInfo(vrmMeta: springBone.gameObject.GetComponentsInParent<VRMMeta>()[0]);

            foreach (var transform in springBone.RootBones)
            {
                var dynamicBone = springBone.gameObject.AddComponent(DynamicBoneType);
                DynamicBoneType.GetField("m_Root").SetValue(dynamicBone, transform);
                DynamicBoneType.GetField("m_Exclusions").SetValue(dynamicBone, new List<Transform>());

                DynamicBoneParameters dynamicBoneParameters = null;
                if (swayingParametersConverter != null)
                {
                    dynamicBoneParameters = swayingParametersConverter(
                        springBoneParameters: springBoneParameters,
                        boneInfo: boneInfo
                    );
                }
                if (dynamicBoneParameters != null)
                {
                    DynamicBoneType.GetField("m_Damping").SetValue(dynamicBone, dynamicBoneParameters.Damping);
                    DynamicBoneType.GetField("m_DampingDistrib")
                        .SetValue(dynamicBone, dynamicBoneParameters.DampingDistrib);
                    DynamicBoneType.GetField("m_Elasticity").SetValue(dynamicBone, dynamicBoneParameters.Elasticity);
                    DynamicBoneType.GetField("m_ElasticityDistrib")
                        .SetValue(dynamicBone, dynamicBoneParameters.ElasticityDistrib);
                    DynamicBoneType.GetField("m_Stiffness").SetValue(dynamicBone, dynamicBoneParameters.Stiffness);
                    DynamicBoneType.GetField("m_StiffnessDistrib")
                        .SetValue(dynamicBone, dynamicBoneParameters.StiffnessDistrib);
                    DynamicBoneType.GetField("m_Inert").SetValue(dynamicBone, dynamicBoneParameters.Inert);
                    DynamicBoneType.GetField("m_InertDistrib")
                        .SetValue(dynamicBone, dynamicBoneParameters.InertDistrib);
                }

                DynamicBoneType.GetField("m_Gravity")
                    .SetValue(dynamicBone, springBone.m_gravityDir * springBone.m_gravityPower);
                DynamicBoneType.GetField("m_Radius").SetValue(dynamicBone, springBone.m_hitRadius);
                if (dynamicBoneColliderGroups != null)
                {
                    var colliders = Activator.CreateInstance(type: DynamicBoneColliderBaseListType);
                    MethodInfo addMethod = DynamicBoneColliderBaseListType.GetMethod("Add");
                    foreach (var collider in springBone.ColliderGroups.SelectMany(
                            colliderGroup => dynamicBoneColliderGroups[colliderGroup]
                    ))
                    {
                        addMethod.Invoke(colliders, new[] { collider });
                    }
                    DynamicBoneType.GetField("m_Colliders").SetValue(dynamicBone, colliders);
                }
            }
        }

        /// <summary>
        /// DynamicBoneの制限の既定値を超えていた場合、警告メッセージを返します。
        /// </summary>
        /// <seealso cref="AvatarPerformance.AnalyzeDynamicBone"/>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static IEnumerable<Converter.Message> GetMessagesAboutDynamicBoneLimits(GameObject avatar)
        {
            var messages = new List<Converter.Message>();
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            AvatarPerformanceStats statistics = new AvatarPerformanceStats();
            AvatarPerformance.CalculatePerformanceStats(avatar.GetComponent<VRMMeta>().Meta.Title, avatar, statistics);

            AvatarPerformanceStatsLevel mediumPerformanceStatLimits
                = VRChatUtility.AvatarPerformanceStatsLevelSets["PC"].medium;

            if (statistics.dynamicBoneSimulatedBoneCount > mediumPerformanceStatLimits.dynamicBoneSimulatedBoneCount)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(
                        _("The “Dynamic Bone Simulated Bone Count” is {0}."),
                        statistics.dynamicBoneSimulatedBoneCount
                    ) + string.Format(
                        _("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                        mediumPerformanceStatLimits.dynamicBoneSimulatedBoneCount
                    ),
                    type = MessageType.Warning,
                });
            }

            if (statistics.dynamicBoneCollisionCheckCount > mediumPerformanceStatLimits.dynamicBoneCollisionCheckCount)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(
                        _("The “Dynamic Bone Collision Check Count” is {0}."),
                        statistics.dynamicBoneCollisionCheckCount
                    ) + string.Format(
                        _("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                        mediumPerformanceStatLimits.dynamicBoneCollisionCheckCount
                    ),
                    type = MessageType.Warning,
                });
            }
#endif

            return messages;
        }
    }
}
