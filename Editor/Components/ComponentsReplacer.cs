using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniGLTF;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.VRChatUtility;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// キャラクター情報、視点、揺れ物に関する設定。
    /// </summary>
    internal class ComponentsReplacer
    {
        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <returns></returns>
        internal static IEnumerable<(string, MessageType)> Apply(
            GameObject avatar,
            Converter.SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            Converter.SwayingParametersConverter swayingParametersConverter
        )
        {
            var messages = new List<(string, MessageType)>();

            ConvertMeta(avatar: avatar);
            ConvertVRMFirstPerson(avatar: avatar);

            if (DynamicBoneType == null
                || swayingObjectsConverterSetting == Converter.SwayingObjectsConverterSetting.RemoveSwayingObjects)
            {
                return messages;
            }

            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<dynamic>> dynamicBoneColliderGroups = null;
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

            messages.AddRange(VRChatUtility.CalculateDynamicBoneLimitations(prefabInstance: avatar));

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
            var avatarDescriptor
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                = avatar.GetComponent<VRC_AvatarDescriptor>();
#else
                = (dynamic)null;
#endif
            var firstPerson = avatar.GetComponent<VRMFirstPerson>();
            avatarDescriptor.ViewPosition = firstPerson.FirstPersonBone.position + firstPerson.FirstPersonOffset - avatar.transform.localPosition;
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
        private static IDictionary<VRMSpringBoneColliderGroup, IEnumerable<dynamic>> ConvertVRMSpringBoneColliderGroups(GameObject avatar)
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
        private static IEnumerable<dynamic> ConvertVRMSpringBoneColliderGroup(
            VRMSpringBoneColliderGroup colliderGroup
        )
        {
            var bone = colliderGroup.gameObject;

            return colliderGroup.Colliders.Select(collider =>
            {
                dynamic dynamicBoneCollider = bone.AddComponent(DynamicBoneColliderType);
                dynamicBoneCollider.m_Center = collider.Offset;
                dynamicBoneCollider.m_Radius = collider.Radius;
                return dynamicBoneCollider;
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
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<dynamic>> dynamicBoneColliderGroups,
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
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<dynamic>> dynamicBoneColliderGroups,
            Converter.SwayingParametersConverter swayingParametersConverter
        )
        {
            var springBoneParameters = new SpringBoneParameters()
            {
                StiffnessForce = springBone.m_stiffnessForce,
                DragForce = springBone.m_dragForce,
            };
            var boneInfo = new BoneInfo(vrmMeta: springBone.gameObject.GetComponentsInParent<VRMMeta>()[0]);

            foreach (var transform in springBone.RootBones)
            {
                dynamic dynamicBone = springBone.gameObject.AddComponent(DynamicBoneType);
                dynamicBone.m_Root = transform;
                dynamicBone.m_Exclusions = new List<Transform>();

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
                    dynamicBone.m_Damping = dynamicBoneParameters.Damping;
                    dynamicBone.m_DampingDistrib = dynamicBoneParameters.DampingDistrib;
                    dynamicBone.m_Elasticity = dynamicBoneParameters.Elasticity;
                    dynamicBone.m_ElasticityDistrib = dynamicBoneParameters.ElasticityDistrib;
                    dynamicBone.m_Stiffness = dynamicBoneParameters.Stiffness;
                    dynamicBone.m_StiffnessDistrib = dynamicBoneParameters.StiffnessDistrib;
                    dynamicBone.m_Inert = dynamicBoneParameters.Inert;
                    dynamicBone.m_InertDistrib = dynamicBoneParameters.InertDistrib;
                }

                dynamicBone.m_Gravity = springBone.m_gravityDir * springBone.m_gravityPower;
                dynamicBone.m_Radius = springBone.m_hitRadius;
                if (dynamicBoneColliderGroups != null)
                {
                    dynamic colliders = Activator.CreateInstance(type: DynamicBoneColliderBaseListType);
                    foreach (var collider in springBone.ColliderGroups.SelectMany(
                            colliderGroup => dynamicBoneColliderGroups[colliderGroup]
                    ))
                    {
                        colliders.Add(collider);
                    }
                    dynamicBone.m_Colliders = colliders;
                }
            }
        }
    }
}
