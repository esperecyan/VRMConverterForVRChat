using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;
using Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM;

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
            HANDOPEN,
            FIST,
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
        /// Questのデフォルト (アバターパフォーマンスランクMedium) の制限値。
        /// </summary>
        internal static (
            int triangleCount,
            int skinnedMeshCount,
            int meshCount,
            int subMeshCount,
            int boneCount,
            int vrcPhysBoneCount,
            int vrcPhysBoneAffectedTransformCount,
            int vrcPhysBoneColliderCount,
            int vrcPhysBoneCollisionCheckCount
        ) QuestMediumLimitations = (
            triangleCount: 15000,
            skinnedMeshCount: 2,
            meshCount: 2,
            subMeshCount: 2,
            boneCount: 150,
            vrcPhysBoneCount: 6,
            vrcPhysBoneAffectedTransformCount: 32,
            vrcPhysBoneColliderCount: 8,
            vrcPhysBoneCollisionCheckCount: 32
        );

        /// <summary>
        /// Questのハードリミット値 (VRCPhysBone関係のみ)。
        /// </summary>
        internal static (
            int vrcPhysBoneCount,
            int vrcPhysBoneAffectedTransformCount,
            int vrcPhysBoneColliderCount,
            int vrcPhysBoneCollisionCheckCount
        ) QuestPoorLimitations = (
            vrcPhysBoneCount: 8,
            vrcPhysBoneAffectedTransformCount: 64,
            vrcPhysBoneColliderCount: 16,
            vrcPhysBoneCollisionCheckCount: 64
        );

        /// <summary>
        /// <see cref="ExpressionPreset"/>と<see cref="VRC_AvatarDescriptor.VisemeBlendShapes"/>のインデックスの対応関係。
        /// </summary>
        internal static readonly IDictionary<ExpressionPreset, int> ExpressionPresetVRChatVisemeIndexPairs
            = new Dictionary<ExpressionPreset, int>()
            {
                { ExpressionPreset.Aa, 10 },
                { ExpressionPreset.Ih, 12 },
                { ExpressionPreset.Ou, 14 },
                { ExpressionPreset.Ee, 11 },
                { ExpressionPreset.Oh, 13 },
            };

        private static readonly Regex MybeBlinkShapeKeyNamePattern = new Regex(
            "blink|まばたき|またたき|瞬き|eye|目|瞳|眼|wink|ウィンク|ｳｨﾝｸ|ウインク|ｳｲﾝｸ",
            RegexOptions.IgnoreCase
        );

        /// <summary>
        /// 【SDK2】Cats Blender PluginでVRChat用に生成されるまばたきのシェイプキー名。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// cats-blender-plugin/eyetracking.py at 0.13.3 · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/0.13.3/tools/eyetracking.py>
        /// </remarks>
        private static readonly IEnumerable<string> OrderedBlinkGeneratedByCatsBlenderPlugin
            = new string[] { "vrc.blink_left", "vrc.blink_right", "vrc.lowerlid_left", "vrc.lowerlid_right" };

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
            "VRC.SDK3.VRCTestMarker",
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor",
            "VRC.SDK3.Avatars.Components.VRCStation",
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone",
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider",
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender",
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver",
        };

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
        /// VRChatのアバターで使用されているシェイプキー・アニメーションから、Expressionに関するものを検出します。
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="shapeKeyNames"></param>
        /// <returns></returns>
        internal static (
            IEnumerable<AnimationClip> animations,
            IDictionary<ExpressionPreset, VRChatExpressionBinding> expressions
        ) DetectVRChatExpressions(GameObject instance, IEnumerable<string> shapeKeyNames)
        {
            var animations = new List<AnimationClip>();
            var expressions = new Dictionary<ExpressionPreset, VRChatExpressionBinding>();

            var avatarDescriptor = instance.GetComponent<VRCAvatarDescriptor>();
            var visemes = avatarDescriptor.VisemeBlendShapes;
            if (visemes != null)
            {
                foreach (var (preset, index) in VRChatUtility.ExpressionPresetVRChatVisemeIndexPairs)
                {
                    var shapeKeyName = visemes.ElementAtOrDefault(index);
                    if (shapeKeyName == null || !shapeKeyNames.Contains(shapeKeyName))
                    {
                        continue;
                    }
                    expressions[preset] = new VRChatExpressionBinding() { ShapeKeyNames = new[] { shapeKeyName } };
                }
            }
            var controller = avatarDescriptor.baseAnimationLayers
                .FirstOrDefault(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController;
            if (controller != null)
            {
                animations = controller.animationClips.ToList();
            }

            VRChatUtility.DetectBlinkExpressions(expressions, instance, shapeKeyNames);

            return (animations, expressions);
        }

        /// <summary>
        /// まばたきに関係している可能性があるシェイプキーを返します。
        /// </summary>
        /// <returns></returns>
        internal static IEnumerable<string> DetectBlinkShapeKeyNames(IEnumerable<string> shapeKeyNames)
        {
            return shapeKeyNames
                .Where(shapeKeyName => VRChatUtility.MybeBlinkShapeKeyNamePattern.IsMatch(shapeKeyName));
        }

        /// <summary>
        /// まばたきのシェイプキーを検出します。
        /// </summary>
        /// <param name="expressions"></param>
        /// <param name="instance"></param>
        /// <param name="shapeKeyNames"></param>
        /// <returns></returns>
        private static void DetectBlinkExpressions(
            IDictionary<ExpressionPreset, VRChatExpressionBinding> expressions,
            GameObject instance,
            IEnumerable<string> shapeKeyNames
        )
        {
            // ダミーの可能性があるシェイプキー
            var maybeDummyBlinkShapeKeyNames = new List<string>();
            var body = instance.transform.Find("Body");
            if (body != null)
            {
                var renderer = body.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount >= 4)
                {
                    maybeDummyBlinkShapeKeyNames.Add(renderer.sharedMesh.GetBlendShapeName(0));
                    maybeDummyBlinkShapeKeyNames.Add(renderer.sharedMesh.GetBlendShapeName(1));
                }
            }
            maybeDummyBlinkShapeKeyNames.AddRange(shapeKeyNames.Where(
                shapeKeyName => VRChatUtility.OrderedBlinkGeneratedByCatsBlenderPlugin.Contains(shapeKeyName)
            ));

            var settings = instance.GetComponent<VRCAvatarDescriptor>().customEyeLookSettings;
            if (settings.eyelidsSkinnedMesh != null && settings.eyelidsSkinnedMesh.sharedMesh != null
                && settings.eyelidsBlendshapes != null && settings.eyelidsBlendshapes.Count() == 3
                && settings.eyelidsSkinnedMesh.sharedMesh.blendShapeCount > settings.eyelidsBlendshapes[0])
            {
                expressions[ExpressionPreset.Blink] = new VRChatExpressionBinding() {
                    ShapeKeyNames = new[] {
                        settings.eyelidsSkinnedMesh.sharedMesh.GetBlendShapeName(settings.eyelidsBlendshapes[0]),
                    },
                };
            }

            var blinkShapeKeys = shapeKeyNames.Where(shapeKeyName => shapeKeyName.ToLower().Contains("blink")).ToList();
            if (blinkShapeKeys.Count() > 0)
            {
                if (expressions.ContainsKey(ExpressionPreset.Blink)
                    && blinkShapeKeys.Contains(expressions[ExpressionPreset.Blink].ShapeKeyNames.First()))
                {
                    // SDK3の両目まばたきが設定済みなら、それを取り除く
                    blinkShapeKeys.Remove(expressions[ExpressionPreset.Blink].ShapeKeyNames.First());
                }

                // 片目まばたき
                foreach (var (preset, name) in new Dictionary<ExpressionPreset, string>()
                {
                    { ExpressionPreset.BlinkLeft, "left" },
                    { ExpressionPreset.BlinkRight, "right" },
                })
                {
                    var blinkOneEyeShapeKeyNames = blinkShapeKeys.Where(shapeKeyName => Regex.IsMatch(
                        shapeKeyName,
                        $"(^|[^a-z])${Regex.Escape(name[0].ToString())}?([^a-z]|$)|{Regex.Escape(name)}",
                        RegexOptions.IgnoreCase
                    ));
                    if (blinkOneEyeShapeKeyNames.Count() > 0)
                    {
                        if (blinkOneEyeShapeKeyNames.Count() > 1)
                        {
                            var mayblinkOneEyeShapeKeyNames = blinkOneEyeShapeKeyNames.Except(maybeDummyBlinkShapeKeyNames);
                            if (mayblinkOneEyeShapeKeyNames.Count() > 1)
                            {
                                blinkOneEyeShapeKeyNames = mayblinkOneEyeShapeKeyNames;
                            }
                        }
                        expressions[preset] = new VRChatExpressionBinding()
                        {
                            ShapeKeyNames = new[] { blinkOneEyeShapeKeyNames.First() },
                        };
                    }
                }

                if (!expressions.ContainsKey(ExpressionPreset.Blink))
                {
                    // SDK3の両目まばたきが未設定なら
                    var blinkBothEyesShapeKeyName = blinkShapeKeys.FirstOrDefault(shapeKeyName =>
                        !Regex.IsMatch(shapeKeyName, "(^|[^a-z])[lr]([^a-z]|$)|left|right", RegexOptions.IgnoreCase));
                    if (blinkBothEyesShapeKeyName != null)
                    {
                        // 両目シェイプキーでないもの
                        expressions[ExpressionPreset.Blink]
                            = new VRChatExpressionBinding() { ShapeKeyNames = new[] { blinkBothEyesShapeKeyName } };
                    }
                    else
                    {
                        // 片目シェイプキーを組み合わせる
                        var blinkOneEyeShapeKeyNames = new[] { ExpressionPreset.BlinkLeft, ExpressionPreset.BlinkRight }
                            .Where(preset => expressions.ContainsKey(preset))
                            .Select(preset => expressions[preset].ShapeKeyNames.First());
                        if (blinkOneEyeShapeKeyNames.Count() > 1)
                        {
                            expressions[ExpressionPreset.Blink]
                                = new VRChatExpressionBinding() { ShapeKeyNames = blinkOneEyeShapeKeyNames };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// QuestのVRCPhysBoneの制限の既定値を超えていた場合は警告メッセージ、ハードリミットを超えていた場合はエラーメッセージを返します。
        /// </summary>
        /// <param name="prefabInstance"></param>
        /// <returns></returns>
        internal static IEnumerable<(string, MessageType)> CalculateQuestVRCPhysBoneLimitations(GameObject prefabInstance)
        {
            var messages = new List<(string, MessageType)>();

            var vrcPhysBones = prefabInstance.GetComponentsInChildren<VRCPhysBone>();
            if (vrcPhysBones.Length > VRChatUtility.QuestPoorLimitations.vrcPhysBoneCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Components” is {0}."),
                    vrcPhysBones.Length
                ) + " " + string.Format(
                    _("If this value exceeds {0}, you cannot upload your avatar."),
                    VRChatUtility.QuestPoorLimitations.vrcPhysBoneCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneCount
                ), MessageType.Error));
            }
            else if (vrcPhysBones.Length > VRChatUtility.QuestMediumLimitations.vrcPhysBoneCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Components” is {0}."),
                    vrcPhysBones.Length
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneCount
                ), MessageType.Warning));
            }

            var vrcPhysBoneAffectedTransformCountPairs = vrcPhysBones.ToDictionary(
                vrcPhysBone => vrcPhysBone,
                vrcPhysBone =>
                {
                    var root = vrcPhysBone.rootTransform;
                    if (!root.IsChildOf(prefabInstance.transform))
                    {
                        return 0;
                    }

                    var exclusions = vrcPhysBone.ignoreTransforms;
                    return root.GetComponentsInChildren<Transform>().Length
                        //- 1 // Collision checks counted incorrectly | Bug Reports | VRChat <https://vrchat.canny.io/bug-reports/p/collision-checks-counted-incorrectly>
                        - (exclusions != null
                            ? exclusions.Sum(exclusion => exclusion.GetComponentsInChildren<Transform>().Length)
                            : 0);
                }
            );
            var affectedTransformCount = vrcPhysBoneAffectedTransformCountPairs.Values.Sum();

            if (affectedTransformCount > VRChatUtility.QuestPoorLimitations.vrcPhysBoneAffectedTransformCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Simulated Bone Count” (Number of bones including root in VRCPhysBone) is {0}."),
                    affectedTransformCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, you cannot upload your avatar."),
                    VRChatUtility.QuestPoorLimitations.vrcPhysBoneAffectedTransformCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneAffectedTransformCount
                ), MessageType.Error));
            }
            else if (affectedTransformCount > VRChatUtility.QuestMediumLimitations.vrcPhysBoneAffectedTransformCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Simulated Bone Count” (Number of bones including root in VRCPhysBone) is {0}."),
                    affectedTransformCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneAffectedTransformCount
                ), MessageType.Warning));
            }

            var vrcPhysBoneColliderCount = prefabInstance.GetComponentsInChildren<VRCPhysBoneCollider>().Length;
            if (vrcPhysBoneColliderCount > VRChatUtility.QuestPoorLimitations.vrcPhysBoneColliderCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Colliders” is {0}."),
                    vrcPhysBoneColliderCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, you cannot upload your avatar."),
                    VRChatUtility.QuestPoorLimitations.vrcPhysBoneColliderCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneColliderCount
                ), MessageType.Error));
            }
            else if (vrcPhysBoneColliderCount > VRChatUtility.QuestMediumLimitations.vrcPhysBoneColliderCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Colliders” is {0}."),
                    vrcPhysBoneColliderCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneColliderCount
                ), MessageType.Warning));
            }

            var collisionCheckCount = vrcPhysBoneAffectedTransformCountPairs.Sum(vrcPhysBoneAffectedTransformCountPair =>
            {
                var colliders = vrcPhysBoneAffectedTransformCountPair.Key.colliders;
                if (colliders == null)
                {
                    return 0;
                }

                return (vrcPhysBoneAffectedTransformCountPair.Value - 1 /* ルートボーンを除外 */)
                    * colliders.Where(collider => collider.transform.IsChildOf(prefabInstance.transform)).Count();
            });
            if (collisionCheckCount > VRChatUtility.QuestPoorLimitations.vrcPhysBoneCollisionCheckCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Collision Check Count” (Number of bones excluding root in VRCPhysBone × Number of VRCPhysBoneCollider) is {0}."),
                    collisionCheckCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, you cannot upload your avatar."),
                    VRChatUtility.QuestPoorLimitations.vrcPhysBoneCollisionCheckCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneCollisionCheckCount
                ), MessageType.Error));
            }
            else if (collisionCheckCount > VRChatUtility.QuestMediumLimitations.vrcPhysBoneCollisionCheckCount)
            {
                messages.Add((string.Format(
                    _("The “PhysBones Collision Check Count” (Number of bones excluding root in VRCPhysBone × Number of VRCPhysBoneCollider) is {0}."),
                    collisionCheckCount
                ) + " " + string.Format(
                    _("If this value exceeds {0}, all VRCPhysBones will be disabled under the default user setting."),
                    VRChatUtility.QuestMediumLimitations.vrcPhysBoneCollisionCheckCount
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
                    limit: VRChatUtility.QuestMediumLimitations.skinnedMeshCount,
                    message: _("The number of Skinned Mesh Renderer components is {0}.")
                ),
                (
                    current: VRChatUtility.CountStaticMesh(prefabInstance),
                    limit: VRChatUtility.QuestMediumLimitations.meshCount,
                    message: _("The number of (non-Skinned) Mesh Renderer components is {0}.")
                ),
                (
                    current: VRChatUtility.CountSubMesh(prefabInstance),
                    limit: VRChatUtility.QuestMediumLimitations.subMeshCount,
                    message: _("The number of material slots (sub-meshes) is {0}.")
                ),
                (
                    current: VRChatUtility.CountBone(prefabInstance),
                    limit: VRChatUtility.QuestMediumLimitations.boneCount,
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
            return VRChatUtility.GetMeshes(prefabInstance).Sum(mesh => mesh.triangles.Count() / 3);
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
