using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UniGLTF;
using VRM;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.VRChatUtility;

namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// DynamicBoneをVRMSpringBoneへ変換します。
    /// </summary>
    internal class DynamicBoneReplacer
    {
        /// <summary>
        /// 揺れ物のパラメータ設定を行うコールバック関数。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        internal delegate SpringBoneParameters ParametersConverter(
            DynamicBoneParameters dynamicBoneParameters,
            BoneInfo boneInfo
        );

        internal static void SetSpringBonesAndColliders(
            GameObject instance,
            ParametersConverter swayingParametersConverter
        )
        {
            if (DynamicBoneType != null)
            {
                DynamicBoneReplacer.SetSpringBoneColliderGroups(instance);
                DynamicBoneReplacer.SetSpringBones(instance, swayingParametersConverter);
                DynamicBoneReplacer.RemoveUnusedColliderGroups(instance);
            }
            DynamicBoneReplacer.SetSpringBoneColliderGroupsForVirtualCast(instance);
        }

        /// <summary>
        /// 子孫の<see cref="DynamicBoneCollider"/>を基に<see cref="VRMSpringBoneColliderGroup"/>を設定します。
        /// </summary>
        /// <param name="instance"></param>
        private static void SetSpringBoneColliderGroups(GameObject instance)
        {
            foreach (var colliders in instance.GetComponentsInChildren(DynamicBoneColliderType)
                .Where(collider =>
                {
                    if ((int)((dynamic)collider).m_Bound != 0)
                    {
                        Debug.LogWarning("インサイドコライダーは変換できません: "
                            + collider.transform.RelativePathFrom(instance.transform));
                        return false;
                    }
                    return true;
                })
                .GroupBy(collider => collider.transform.gameObject))
            {
                colliders.Key.AddComponent<VRMSpringBoneColliderGroup>().Colliders
                    = colliders.SelectMany(DynamicBoneReplacer.ConvertCollider).ToArray();
            }
        }

        /// <summary>
        /// 指定された<see cref="DynamicBoneCollider"/>を基に<see cref="SphereCollider"/>を生成します。
        /// </summary>
        /// <param name="colliders"><see cref="DynamicBoneCollider"/></param>
        /// <returns><see cref="DynamicBoneCollider.m_Height"/>が0の場合は1つ、それ以外の場合は3つ。</returns>
        private static IEnumerable<VRMSpringBoneColliderGroup.SphereCollider> ConvertCollider(
            dynamic dynamicBoneCollider
        )
        {
            var centers = new List<Vector3>() { dynamicBoneCollider.m_Center };
            if (dynamicBoneCollider.m_Height > 0)
            {
                var distance = (float)dynamicBoneCollider.m_Height / 2;
                switch ((int)dynamicBoneCollider.m_Direction)
                {
                    case 0: // DynamicBoneColliderBase.Direction.X
                        centers.Add(centers[0] + new Vector3(distance, 0, 0));
                        centers.Add(centers[0] + new Vector3(-distance, 0, 0));
                        break;
                    case 1: // DynamicBoneColliderBase.Direction.Y
                        centers.Add(centers[0] + new Vector3(0, distance, 0));
                        centers.Add(centers[0] + new Vector3(0, -distance, 0));
                        break;
                    case 2: // DynamicBoneColliderBase.Direction.Z
                        centers.Add(centers[0] + new Vector3(0, 0, distance));
                        centers.Add(centers[0] + new Vector3(0, 0, -distance));
                        break;
                }
            }

            return centers.Select(center => new VRMSpringBoneColliderGroup.SphereCollider
            {
                Offset = center,
                Radius = dynamicBoneCollider.m_Radius
            });
        }

        /// <summary>
        /// 子孫の<see cref="DynamicBone"/>を基に<see cref="VRMSpringBone"/>を設定します。
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="swayingParametersConverter"></param>
        private static void SetSpringBones(GameObject instance, ParametersConverter swayingParametersConverter)
        {
            var boneInfo = new BoneInfo(instance.GetComponent<VRMMeta>());
            var secondary = instance.transform.Find("secondary").gameObject;
            GameObject.DestroyImmediate(secondary.GetComponent<VRMSpringBone>());

            foreach (var dynamicBones in instance.GetComponentsInChildren(DynamicBoneType)
                .Select((dynamic dynamicBone) =>
                {
                    var parameters = swayingParametersConverter(new DynamicBoneParameters()
                    {
                        Damping = dynamicBone.m_Damping,
                        DampingDistrib = dynamicBone.m_DampingDistrib,
                        Elasticity = dynamicBone.m_Elasticity,
                        ElasticityDistrib = dynamicBone.m_ElasticityDistrib,
                        Stiffness = dynamicBone.m_Stiffness,
                        StiffnessDistrib = dynamicBone.m_StiffnessDistrib,
                        Inert = dynamicBone.m_Inert,
                        InertDistrib = dynamicBone.m_InertDistrib,
                    }, boneInfo);

                    var colliderGroups = new List<VRMSpringBoneColliderGroup>();
                    if (dynamicBone.m_Colliders != null)
                    {
                        foreach (var collider in dynamicBone.m_Colliders)
                        {
                            // コライダーが削除された、などで消失状態の場合がある
                            if (collider == null)
                            {
                                continue;
                            }
                            if (!collider.transform.IsChildOf(instance.transform))
                            {
                                // ルート外の参照を除外
                                continue;
                            }

                            VRMSpringBoneColliderGroup colliderGroup
                                = collider.GetComponent<VRMSpringBoneColliderGroup>();
                            if (colliderGroup == null || colliderGroups.Contains(colliderGroup))
                            {
                                continue;
                            }

                            colliderGroups.Add(colliderGroup);
                        }
                    }

                    Vector3 gravity = dynamicBone.m_Gravity;
                    return (dynamicBone, parameters, colliderGroups, compare: string.Join("\n", new[]
                    {
                        parameters.StiffnessForce,
                        gravity.x,
                        gravity.y,
                        gravity.z,
                        parameters.DragForce,
                        (float)dynamicBone.m_Radius,
                    }.Select(parameter => parameter.ToString("F2"))
                    .Concat(colliderGroups
                        .Select(colliderGroup => colliderGroup.transform.RelativePathFrom(instance.transform)))
                    ));
                })
                .GroupBy(dynamicBones => dynamicBones.compare)) // 同一パラメータでグループ化
            {
                var dynamicBone = dynamicBones.First();
                var springBone = secondary.AddComponent<VRMSpringBone>();
                springBone.m_stiffnessForce = dynamicBone.parameters.StiffnessForce;
                Vector3 gravity = dynamicBone.dynamicBone.m_Gravity;
                springBone.m_gravityPower = gravity.magnitude;
                springBone.m_gravityDir = gravity.normalized;
                springBone.m_dragForce = dynamicBone.parameters.DragForce;
                springBone.RootBones = dynamicBones.Select(db => (Transform)db.dynamicBone.m_Root)
                    .Where(transform => transform != null && transform.IsChildOf(instance.transform))
                    .Distinct()
                    .ToList();
                springBone.m_hitRadius = dynamicBone.dynamicBone.m_Radius;
                springBone.ColliderGroups = dynamicBone.colliderGroups.ToArray();
            }
        }

        /// <summary>
        /// <see cref="VRMSpringBone.ColliderGroups"/>から参照されていない<see cref="VRMSpringBoneColliderGroup"/>を、
        /// <see cref="HumanBodyBones.LeftHand"/>、<see cref="HumanBodyBones.RightHand"/>を除いて削除します。
        /// </summary>
        /// <param name="instance"></param>
        private static void RemoveUnusedColliderGroups(GameObject instance)
        {
            var animator = instance.GetComponent<Animator>();
            var hands = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand }
                .Select(bone => animator.GetBoneTransform(bone).gameObject);

            var objectsHavingUsedColliderGroup = instance.GetComponentsInChildren<VRMSpringBone>()
                .SelectMany(springBone => springBone.ColliderGroups)
                .Select(colliderGroup => colliderGroup.gameObject)
                .ToArray();

            foreach (var colliderGroup in instance.GetComponentsInChildren<VRMSpringBoneColliderGroup>())
            {
                if (!objectsHavingUsedColliderGroup.Contains(colliderGroup.gameObject)
                    && !hands.Contains(colliderGroup.gameObject))
                {
                    Object.DestroyImmediate(colliderGroup);
                }
            }
        }

        /// <summary>
        /// <see cref="HumanBodyBones.LeftHand"/>、<see cref="HumanBodyBones.RightHand"/>に<see cref="VRMSpringBoneColliderGroup"/>が存在しなければ設定します。
        /// </summary>
        /// <param name="instance"></param>
        private static void SetSpringBoneColliderGroupsForVirtualCast(GameObject instance)
        {
            var animator = instance.GetComponent<Animator>();
            foreach (var bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                var hand = animator.GetBoneTransform(bone);
                if (hand.GetComponent<VRMSpringBoneColliderGroup>() == null)
                {
                    hand.transform.gameObject.AddComponent<VRMSpringBoneColliderGroup>();
                }
            }
        }
    }
}
