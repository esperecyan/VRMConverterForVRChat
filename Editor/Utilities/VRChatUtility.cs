using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#endif
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRC.Core;
#endif
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// VRChat関連の処理など。
    /// </summary>
    internal static class VRChatUtility
    {
        /// <summary>
        /// 表情の設定に利用するアニメーション名。
        /// </summary>
        internal enum Anim
        {
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP,
            FINGERPOINT,
        }

        internal static readonly Type DynamicBoneType = Type.GetType("DynamicBone, Assembly-CSharp");
        internal static readonly Type DynamicBoneColliderType = Type.GetType("DynamicBoneCollider, Assembly-CSharp");
        internal static readonly Type DynamicBoneColliderBaseListType
            = Type.GetType("System.Collections.Generic.List`1[[DynamicBoneColliderBase, Assembly-CSharp]]");

        /// <summary>
        /// アバターの最小の肩の位置。
        /// </summary>
        internal static readonly float MinShoulderHeight = 0.2f;

        /// <summary>
        /// VRChat SDKがサポートするバージョンのUnityのダウンロード先。
        /// </summary>
        internal static readonly string DownloadURL = "https://docs.vrchat.com/docs/current-unity-version";

        /// <summary>
        /// オートアイムーブメントの有効化に必要となるダミーの階層構造。
        /// </summary>
        internal static readonly string[] RequiredPathForAutoEyeMovement = new[] {
            "Armature/Hips/Spine/Chest/Neck/Head/LeftEye",
            "Armature/Hips/Spine/Chest/Neck/Head/RightEye",
        };

        /// <summary>
        /// 自動まばたきに利用されるメッシュのオブジェクトのパス。
        /// </summary>
        internal static readonly string AutoBlinkMeshPath = "Body";

        /// <summary>
        /// アニメーションオーバーライドの有効化に必須となるボーン。
        /// </summary>
        internal static readonly IDictionary<HumanBodyBones, HumanBodyBones[]>
            RequiredHumanBodyBonesForAnimationOverride = new Dictionary<HumanBodyBones, HumanBodyBones[]> {
                { HumanBodyBones.LeftHand, new[] {
                    HumanBodyBones.LeftThumbProximal,
                    HumanBodyBones.LeftIndexProximal,
                    HumanBodyBones.LeftMiddleProximal,
                } },
                { HumanBodyBones.RightHand, new[] {
                    HumanBodyBones.RightThumbProximal,
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightMiddleProximal,
                } },
            };

        /// <summary>
        /// 制限値。
        /// </summary>
        internal static (
            int dynamicBoneAffectedTransformCount,
            int dynamicBoneCollisionCheckCount,
            int triangleCount,
            int skinnedMeshCount,
            int meshCount,
            int subMeshCount,
            int boneCount
        ) Limitations = (
            // PC
            dynamicBoneAffectedTransformCount: 7500,
            dynamicBoneCollisionCheckCount: 8,
            // Quest
            triangleCount: 7500,
            skinnedMeshCount: 2,
            meshCount: 2,
            subMeshCount: 2,
            boneCount: 150
        );

        /// <summary>
        /// VRChat SDK2がインポートされていれば <c>2</c>、SDK3がインポートされていれば <c>3</c>、いずれもインポートされていなければ <c>null</c>。
        /// </summary>
        internal static readonly int? SDKVersion
#if VRC_SDK_VRCSDK3
            = 3;
#elif VRC_SDK_VRCSDK2
            = 2;
#else
            = null;
#endif

        /// <summary>
        /// VRChat SDKに含まれるカスタムアニメーション設定用のテンプレートファイルのGUID。
        /// </summary>
        /// <remarks>
        /// Assets/VRCSDK/Examples2/Animation/SDK2/CustomOverrideEmpty.overrideController
        /// </remarks>
        private static readonly string CustomAnimsTemplateGUID = "4bd8fbaef3c3de041a22200917ae98b8";

        /// <summary>
        /// PCアバターでのみ使用可能なコンポーネント。
        /// </summary>
        private static readonly IEnumerable<string> AllowedComponentTypesPCOnly = new string[]
        {
            "DynamicBone",
            "DynamicBoneCollider",
            "UnityEngine.Cloth",
            "UnityEngine.Light",
            "UnityEngine.BoxCollider",
            "UnityEngine.SphereCollider",
            "UnityEngine.CapsuleCollider",
            "UnityEngine.Rigidbody",
            "UnityEngine.Joint",
            "UnityEngine.Animations.AimConstraint",
            "UnityEngine.Animations.LookAtConstraint",
            "UnityEngine.Animations.ParentConstraint",
            "UnityEngine.Animations.PositionConstraint",
            "UnityEngine.Animations.RotationConstraint",
            "UnityEngine.Animations.ScaleConstraint",
            "UnityEngine.Camera",
            "UnityEngine.AudioSource",
            "ONSPAudioSource",
            "VRCSDK2.VRC_SpatialAudioSource",
            "VRC.SDK3.Avatars.Components.VRCSpatialAudioSource",
        };

        /// <summary>
        /// PC、Quest両方のアバターで使用可能なコンポーネント。
        /// </summary>
        private static readonly IEnumerable<string> AllowedComponentTypes = new string[]
        {
            "UnityEngine.Transform",
            "UnityEngine.Animator",
            "VRC.Core.PipelineManager",
            "RootMotion.FinalIK.IKExecutionOrder",
            "RootMotion.FinalIK.VRIK",
            "RootMotion.FinalIK.FullBodyBipedIK",
            "RootMotion.FinalIK.LimbIK",
            "RootMotion.FinalIK.AimIK",
            "RootMotion.FinalIK.BipedIK",
            "RootMotion.FinalIK.GrounderIK",
            "RootMotion.FinalIK.GrounderFBBIK",
            "RootMotion.FinalIK.GrounderVRIK",
            "RootMotion.FinalIK.GrounderQuadruped",
            "RootMotion.FinalIK.TwistRelaxer",
            "RootMotion.FinalIK.ShoulderRotator",
            "RootMotion.FinalIK.FBBIKArmBending",
            "RootMotion.FinalIK.FBBIKHeadEffector",
            "RootMotion.FinalIK.FABRIK",
            "RootMotion.FinalIK.FABRIKChain",
            "RootMotion.FinalIK.FABRIKRoot",
            "RootMotion.FinalIK.CCDIK",
            "RootMotion.FinalIK.RotationLimit",
            "RootMotion.FinalIK.RotationLimitHinge",
            "RootMotion.FinalIK.RotationLimitPolygonal",
            "RootMotion.FinalIK.RotationLimitSpline",
            "UnityEngine.SkinnedMeshRenderer",
            "UnityEngine.MeshFilter",
            "UnityEngine.MeshRenderer",
            "UnityEngine.Animation",
            "UnityEngine.ParticleSystem",
            "UnityEngine.ParticleSystemRenderer",
            "UnityEngine.TrailRenderer",
            "UnityEngine.FlareLayer",
            "UnityEngine.GUILayer",
            "UnityEngine.LineRenderer",
            "RealisticEyeMovements.EyeAndHeadAnimator",
            "RealisticEyeMovements.LookTargetController",
            "VRCSDK2.VRC_AvatarDescriptor",
            "VRCSDK2.VRC_AvatarVariations",
            "VRCSDK2.VRC_IKFollower",
            "VRCSDK2.VRC_Station",
            "VRC.SDK3.VRCTestMarker",
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor",
            "VRC.SDK3.Avatars.Components.VRCStation",
        };

        /// <summary>
        /// VRChat SDKがサポートするUnityのバージョンを取得します。
        /// </summary>
        /// <returns>取得できなかった場合は空文字列を返します。</returns>
        internal static string GetSupportedUnityVersion()
        {
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            return RemoteConfig.HasKey("sdkUnityVersion") ? RemoteConfig.GetString("sdkUnityVersion") : "";
#else
            return "";
#endif
        }

        /// <summary>
        /// アバターで使用できないコンポーネントを削除します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <param name="forQeust"></param>
        internal static void RemoveBlockedComponents(GameObject prefabInstance, bool forQeust)
        {
            var allowedComponentTypes = VRChatUtility.AllowedComponentTypes;
            if (!forQeust)
            {
                allowedComponentTypes = allowedComponentTypes.Union(VRChatUtility.AllowedComponentTypesPCOnly);
            }

            foreach (var component in prefabInstance.GetComponentsInChildren<Component>(true)
                .Where(component => !allowedComponentTypes.Contains(component.GetType().FullName)))
            {
                if (component is Camera)
                {
                    Object.DestroyImmediate(component.GetComponent<FlareLayer>());
                }
                Object.DestroyImmediate(component);
            }
        }

        /// <summary>
        /// DynamicBoneの制限の既定値を超えていた場合、警告メッセージを返します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        internal static IEnumerable<(string, MessageType)> CalculateDynamicBoneLimitations(GameObject prefabInstance)
        {
            var messages = new List<(string, MessageType)>();

            var dynamicBoneAffectedTransformCountPairs
                = prefabInstance.GetComponentsInChildren(DynamicBoneType).ToDictionary(
                    dynamicBone => dynamicBone,
                    (dynamic dynamicBone) =>
                    {
                        var root = (Transform)dynamicBone.m_Root;
                        if (!root.IsChildOf(prefabInstance.transform))
                        {
                            return 0;
                        }

                        var exclusions = (List<Transform>)dynamicBone.m_Exclusions;
                        return root.GetComponentsInChildren<Transform>().Length
                            //- 1 // Collision checks counted incorrectly | Bug Reports | VRChat <https://vrchat.canny.io/bug-reports/p/collision-checks-counted-incorrectly>
                            - (exclusions != null
                                ? exclusions.Sum(exclusion => exclusion.GetComponentsInChildren<Transform>().Length)
                                : 0);
                    }
                );
            var affectedTransformCount = dynamicBoneAffectedTransformCountPairs.Values.Sum();

            if (affectedTransformCount > VRChatUtility.Limitations.dynamicBoneAffectedTransformCount)
            {
                messages.Add((string.Format(
                    _("The “Dynamic Bone Simulated Bone Count” is {0}."),
                    affectedTransformCount
                ) + string.Format(
                    _("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                    VRChatUtility.Limitations.dynamicBoneAffectedTransformCount
                ), MessageType.Warning));
            }

            var collisionCheckCount = dynamicBoneAffectedTransformCountPairs.Sum(dynamicBoneAffectedTransformCountPair =>
            {
                var colliders = dynamicBoneAffectedTransformCountPair.Key.m_Colliders;
                if (colliders == null)
                {
                    return 0;
                }

                return dynamicBoneAffectedTransformCountPair.Value
                    * ((IEnumerable<Component>)colliders).Where(collider => collider.transform.IsChildOf(prefabInstance.transform)).Count();
            });
            if (collisionCheckCount > VRChatUtility.Limitations.dynamicBoneCollisionCheckCount)
            {
                messages.Add((string.Format(
                    _("The “Dynamic Bone Collision Check Count” is {0}."),
                    collisionCheckCount
                ) + string.Format(
                    _("If this value exceeds {0}, the default user setting disable all Dynamic Bones."),
                    VRChatUtility.Limitations.dynamicBoneCollisionCheckCount
                ), MessageType.Warning));
            }

            return messages;
        }

        /// <summary>
        /// Questの各制限の既定値を超えていた場合、エラーメッセージを返します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        internal static IEnumerable<(string, MessageType)> CalculateQuestLimitations(GameObject prefabInstance)
        {
            var messages = new List<(string, MessageType)>();

            foreach (var (current, limit, message) in new[] {
                (
                    current: VRChatUtility.CountSkinnedMesh(prefabInstance),
                    limit: VRChatUtility.Limitations.skinnedMeshCount,
                    message: _("The number of Skinned Mesh Renderer components is {0}.")
                ),
                (
                    current: VRChatUtility.CountStaticMesh(prefabInstance),
                    limit: VRChatUtility.Limitations.meshCount,
                    message: _("The number of (non-Skinned) Mesh Renderer components is {0}.")
                ),
                (
                    current: VRChatUtility.CountSubMesh(prefabInstance),
                    limit: VRChatUtility.Limitations.subMeshCount,
                    message: _("The number of material slots (sub-meshes) is {0}.")
                ),
                (
                    current: VRChatUtility.CountBone(prefabInstance),
                    limit: VRChatUtility.Limitations.boneCount,
                    message: _("The number of Bones is {0}.")
                ),
            })
            {
                if (current > limit)
                {
                    messages.Add((string.Format(message, current) + string.Format(
                        _("If this value exceeds {0}, the avatar will not shown under the default user setting."),
                        limit
                    ), MessageType.Error));
                }
            }

            return messages;
        }

        /// <summary>
        /// 三角ポリゴン数を計算します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        internal static int CountTriangle(GameObject prefabInstance)
        {
            return VRChatUtility.GetMeshes(prefabInstance).Sum(mesh => mesh.triangles.Count());
        }

        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.CustomStandingAnims"/>、および<see cref="VRC_AvatarDescriptor.CustomSittingAnims"/>を作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        internal static void AddCustomAnims(GameObject avatar)
        {
            var template = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                AssetDatabase.GUIDToAssetPath(VRChatUtility.CustomAnimsTemplateGUID)
            );
#if VRC_SDK_VRCSDK2
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            if (!avatarDescriptor.CustomStandingAnims)
            {
                avatarDescriptor.CustomStandingAnims = Duplicator.DuplicateAssetToFolder<AnimatorOverrideController>(
                    source: template,
                    prefabInstance: avatar,
                    fileName: "CustomStandingAnims.overrideController"
                );
            }

            if (!avatarDescriptor.CustomSittingAnims)
            {
                avatarDescriptor.CustomSittingAnims = Duplicator.DuplicateAssetToFolder<AnimatorOverrideController>(
                    source: template,
                    prefabInstance: avatar,
                    fileName: "CustomSittingAnims.overrideController"
                );
            }
#endif
        }

        /// <summary>
        /// 指定したオブジェクト内のすべてのメッシュを取得します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static IEnumerable<Mesh> GetMeshes(GameObject prefabInstance)
        {
            return prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)
                .Union(prefabInstance.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh))
                .Where(mesh => mesh != null);
        }

        /// <summary>
        /// SkinnedMesh数を計算します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static int CountSkinnedMesh(GameObject prefabInstance)
        {
            return prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count();
        }

        /// <summary>
        /// Skinnedでないメッシュ数を計算します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static int CountStaticMesh(GameObject prefabInstance)
        {
            return prefabInstance.GetComponentsInChildren<MeshFilter>(true).Count();
        }

        /// <summary>
        /// サブメッシュ数を計算します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static int CountSubMesh(GameObject prefabInstance)
        {
            return VRChatUtility.GetMeshes(prefabInstance).Sum(mesh => mesh.subMeshCount);
        }

        /// <summary>
        /// ボーン数を計算します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        private static int CountBone(GameObject prefabInstance)
        {
            return prefabInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(renderer => renderer.bones)
                .Where(bone => bone != null)
                .Distinct()
                .Count();
        }
    }
}
