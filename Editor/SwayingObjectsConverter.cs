using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// 揺れ物に関する設定。
    /// </summary>
    public class SwayingObjectsConverter
    {
        internal static IEnumerable<Converter.Message> Apply(
            GameObject avatar,
            ComponentsReplacer.SwayingObjectsConverterSetting setting,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter
        )
        {
            if (setting == ComponentsReplacer.SwayingObjectsConverterSetting.RemoveSwayingObjects)
            {
                return new List<Converter.Message>();
            }

            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<DynamicBoneColliderBase>> dynamicBoneColliderGroups = null;
            if (setting == ComponentsReplacer.SwayingObjectsConverterSetting.ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups)
            {
                RemoveUnusedColliderGroups(avatar: avatar);
                dynamicBoneColliderGroups = ConvertVRMSpringBoneColliderGroups(avatar);
            }
            ConvertVRMSpringBones(avatar: avatar, dynamicBoneColliderGroups: dynamicBoneColliderGroups, swayingParametersConverter: swayingParametersConverter);

            return GetMessagesAboutDynamicBoneLimits(avatar: avatar);
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
        private static IDictionary<VRMSpringBoneColliderGroup, IEnumerable<DynamicBoneColliderBase>> ConvertVRMSpringBoneColliderGroups(GameObject avatar)
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
        private static IEnumerable<DynamicBoneColliderBase> ConvertVRMSpringBoneColliderGroup(
            VRMSpringBoneColliderGroup colliderGroup
        )
        {
            var bone = colliderGroup.gameObject;

            return colliderGroup.Colliders.Select(collider => {
                var dynamicBoneCollider = bone.AddComponent<DynamicBoneCollider>();
                dynamicBoneCollider.m_Center = collider.Offset;
                dynamicBoneCollider.m_Radius = collider.Radius;
                return dynamicBoneCollider as DynamicBoneColliderBase;
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
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<DynamicBoneColliderBase>> dynamicBoneColliderGroups,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter
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
            IDictionary<VRMSpringBoneColliderGroup, IEnumerable<DynamicBoneColliderBase>> dynamicBoneColliderGroups,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter
        )
        {
            var springBoneParameters = new SpringBoneParameters(stiffnessForce: springBone.m_stiffnessForce, dragForce: springBone.m_dragForce);
            var boneInfo = new BoneInfo(vrmMeta: springBone.gameObject.GetComponentsInParent<VRMMeta>()[0]);

            foreach (IGrouping<Transform, Transform> parentAndRootBones in springBone.RootBones.ToLookup(keySelector: rootBone => rootBone.parent))
            {
                var dynamicBone = springBone.gameObject.AddComponent<DynamicBone>();
                dynamicBone.m_Root = parentAndRootBones.Key;
                dynamicBone.m_Exclusions = new List<Transform>();
                dynamicBone.m_Exclusions.AddRange(parentAndRootBones.Key.Cast<Transform>().Where(child => !parentAndRootBones.Contains(child)));

                DynamicBoneParameters dynamicBoneParameters = (swayingParametersConverter ?? ComponentsReplacer.DefaultSwayingParametersConverter)(
                    springBoneParameters: springBoneParameters,
                    boneInfo: boneInfo
                );
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
                if (dynamicBoneColliderGroups != null)
                {
                    dynamicBone.m_Colliders = new List<DynamicBoneColliderBase>();
                    dynamicBone.m_Colliders.AddRange(springBone.ColliderGroups.SelectMany(colliderGroup => dynamicBoneColliderGroups[colliderGroup]).ToList());
                }
                dynamicBone.m_DistantDisable = true;
            }
        }


        /// <summary>
        /// DynamicBoneの制限の既定値を超えていた場合、警告メッセージを返します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static IEnumerable<Converter.Message> GetMessagesAboutDynamicBoneLimits(GameObject avatar)
        {
            var messages = new List<Converter.Message>();

            var dynamicBonesAndAffectedTransformCounts = avatar.GetComponentsInChildren<DynamicBone>().ToDictionary(
                keySelector: dynamicBone => dynamicBone,
                elementSelector: dynamicBone => {
                    Transform rootTransform = dynamicBone.m_Root;
                    if (!rootTransform.IsChildOf(avatar.transform))
                    {
                        return 0;
                    }

                    return dynamicBone.m_Root.GetComponentsInChildren<Transform>().Length
                        //- 1 // Collision checks counted incorrectly | Bug Reports | VRChat <https://vrchat.canny.io/bug-reports/p/collision-checks-counted-incorrectly>
                        - dynamicBone.m_Exclusions.Sum(exclusion => exclusion.GetComponentsInChildren<Transform>().Length);
                }
            );

            int affectedTransformCount = dynamicBonesAndAffectedTransformCounts.Values.Sum();
            if (affectedTransformCount > AvatarPerformanceStats.MediumPeformanceStatLimits.DynamicBoneAffectedTransformCount)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(Gettext._("The “Dynamic Bone Affected Transform Count” is {0}."), affectedTransformCount)
                        + string.Format(
                            Gettext._("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                            AvatarPerformanceStats.MediumPeformanceStatLimits.DynamicBoneAffectedTransformCount
                        ),
                    type = MessageType.Warning,
                });
            }

            int collisionCheckCount = dynamicBonesAndAffectedTransformCounts.Sum(selector: dynamicBoneAndAffectedTransformCount => {
                IEnumerable<DynamicBoneColliderBase> colliders = dynamicBoneAndAffectedTransformCount.Key.m_Colliders;
                if (colliders == null)
                {
                    return 0;
                }

                return (dynamicBoneAndAffectedTransformCount.Value)
                    * colliders.Where(collider => collider.transform.IsChildOf(avatar.transform)).Count();
            });
            if (collisionCheckCount > AvatarPerformanceStats.MediumPeformanceStatLimits.DynamicBoneCollisionCheckCount)
            {
                messages.Add(new Converter.Message
                {
                    message = string.Format(Gettext._("The “Dynamic Bone Collision Check Count” is {0}."), collisionCheckCount)
                        + string.Format(
                            Gettext._("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                            AvatarPerformanceStats.MediumPeformanceStatLimits.DynamicBoneCollisionCheckCount
                        ),
                    type = MessageType.Warning,
                });
            }

            return messages;
        }
    }
}
