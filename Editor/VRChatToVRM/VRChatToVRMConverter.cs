using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UniGLTF;
using VRM;
using Esperecyan.UniVRMExtensions;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using SkinnedMeshUtility = Esperecyan.Unity.VRMConverterForVRChat.Utilities.SkinnedMeshUtility;
using Esperecyan.Unity.VRMConverterForVRChat.Components;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif

namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// VRChatのアバターからVRMへアバターの変換を行うパブリックAPI。
    /// </summary>
    internal class VRChatToVRMConverter
    {
        private static readonly string TemporaryFolderPath = "Assets/VRMConverterTemporary";
        private static readonly string TemporaryPrefabFileName = "temporary.prefab";
        private static readonly IEnumerable<string> VRMSupportedShaderNames = new[]
        {
            "Standard",
            "Standard (Specular setup)",
            "Unlit/Color",
            "Unlit/Texture",
            "Unlit/Transparent",
            "Unlit/Transparent Cutout",
            "UniGLTF/NormalMapDecoder",
            "UniGLTF/NormalMapEncoder",
            "UniGLTF/StandardVColor",
            "UniGLTF/UniUnlit",
            "VRM/MToon",
            "VRM/UnlitCutout",
            "VRM/UnlitTexture",
            "VRM/UnlitTransparent",
            "VRM/UnlitTransparentZWrite",
        };

        /// <summary>
        /// <see cref="ComponentsReplacer.SwayingParametersConverter">の既定値。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        internal static SpringBoneParameters DefaultSwayingParametersConverter(
            DynamicBoneParameters dynamicBoneParameters,
            BoneInfo boneInfo
        )
        {
            return new SpringBoneParameters()
            {
                StiffnessForce = dynamicBoneParameters.Elasticity / 0.05f,
                DragForce = dynamicBoneParameters.Damping / 0.6f,
            };
        }

        /// <summary>
        /// VRChatアバターインスタンスからVRMインスタンスへ変換します。
        /// </summary>
        /// <param name="instance">ヒエラルキー上のGameObject。</param>
        /// <param name="presetVRChatBindingPairs">各表情への割り当て。</param>
        /// <param name="swayingParametersConverter"></param>
        internal static void Convert(
            string outputPath,
            GameObject instance,
            VRMMetaObject meta,
            IDictionary<ExpressionPreset, VRChatExpressionBinding> presetVRChatBindingPairs,
            DynamicBoneReplacer.ParametersConverter swayingParametersConverter
        )
        {
            var rootObjectName = instance.name;
            instance = Object.Instantiate(instance);

            // 非表示のオブジェクト・コンポーネントを削除
            // TODO: アクティブ・非アクティブの切り替えをシェイプキーに変換する
            VRChatToVRMConverter.RemoveInactiveObjectsAndDisabledComponents(instance);


            // 表情とシェイプキー名の組み合わせを取得
            var presetShapeKeyNameWeightPairsPairs = presetVRChatBindingPairs.ToDictionary(
                presetVRChatBindingPair => presetVRChatBindingPair.Key,
                presetVRChatBindingPair => VRChatExpressionsReplacer.ExtractShapeKeyNames(presetVRChatBindingPair.Value)
            );

            // VRM設定1
            var temporaryFolder = UnityPath.FromUnityPath(VRChatToVRMConverter.TemporaryFolderPath);
            temporaryFolder.EnsureFolder();
            var temporaryPrefabPath = temporaryFolder.Child(VRChatToVRMConverter.TemporaryPrefabFileName).Value;
            VRMInitializer.Initialize(temporaryPrefabPath, instance);
            VRChatToVRMConverter.SetFirstPersonOffset(instance);
            VRChatToVRMConverter.SetLookAtBoneApplyer(instance);
            DynamicBoneReplacer.SetSpringBonesAndColliders(instance, swayingParametersConverter);

            // 正規化
            var normalized = VRMBoneNormalizer.Execute(instance, forceTPose: true);
            Object.DestroyImmediate(instance);
            instance = normalized;

            // 全メッシュ結合
            var combinedRenderer = CombineMeshesAndSubMeshes.Combine(
                instance,
                notCombineRendererObjectNames: new List<string>(),
                destinationObjectName: "vrm-mesh",
                savingAsAsset: false
            );

            // 使用していないシェイプキーの削除
            SkinnedMeshUtility.CleanUpShapeKeys(combinedRenderer.sharedMesh, presetShapeKeyNameWeightPairsPairs
                .SelectMany(presetShapeKeyNameWeightPairsPair => presetShapeKeyNameWeightPairsPair.Value.Keys)
                .Distinct());

            // シェイプキーの分離
            Utilities.MeshUtility.SeparationProcessing(instance);

            // マテリアルの設定・アセットとして保存
            VRChatToVRMConverter.ReplaceShaders(instance, temporaryPrefabPath);

            // GameObject・メッシュなどをアセットとして保存 (アセットとして存在しないと正常にエクスポートできない)
            instance.name = rootObjectName;
            var animator = instance.GetComponent<Animator>();
            animator.avatar = Duplicator.CreateObjectToFolder(animator.avatar, temporaryPrefabPath);
            meta.name = "Meta";
            instance.GetComponent<VRMMeta>().Meta = Duplicator.CreateObjectToFolder(meta, temporaryPrefabPath);
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMesh.name = renderer.name;
                renderer.sharedMesh = Duplicator.CreateObjectToFolder(renderer.sharedMesh, temporaryPrefabPath);
            }

            // VRM設定2
            VRChatToVRMConverter.SetFirstPersonRenderers(instance);

            // 表情の設定
            VRChatExpressionsReplacer.SetExpressions(instance, presetShapeKeyNameWeightPairsPairs);

            var prefab = PrefabUtility
                .SaveAsPrefabAssetAndConnect(instance, temporaryPrefabPath, InteractionMode.AutomatedAction);

            // エクスポート
            AssetDatabase.SaveAssets();
            VRMEditorExporter.Export(
                outputPath,
                prefab,
                meta: null,
                ScriptableObject.CreateInstance<VRMExportSettings>(),
                info: null
            );
            AssetDatabase.DeleteAsset("Assets/VRMConverterTemporary");
            Object.DestroyImmediate(instance);
        }

        private static void ReplaceShaders(GameObject instance, string temporaryPrefabPath)
        {
            var alreadyDuplicatedMaterials = new Dictionary<Material, Material>();

            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                {
                    if (VRChatToVRMConverter.VRMSupportedShaderNames.Contains(material.shader.name))
                    {
                        return material;
                    }

                    if (alreadyDuplicatedMaterials.ContainsKey(material))
                    {
                        return alreadyDuplicatedMaterials[material];
                    }

                    var newMaterial = Object.Instantiate(material);
                    newMaterial.name = material.name;

                    var shaderName = material.shader.name.ToLower();
                    if (shaderName.Contains("unlit"))
                    {
                        newMaterial.shader = Shader.Find("UniGLTF/UniUnlit");
                    }
                    else if (shaderName.Contains("toon"))
                    {
                        newMaterial.shader = Shader.Find("VRM/MToon");
                    }
                    newMaterial.renderQueue = material.renderQueue;

                    return alreadyDuplicatedMaterials[material]
                        = Duplicator.CreateObjectToFolder(newMaterial, temporaryPrefabPath);
                }).ToArray();
            }
        }

        /// <summary>
        /// 非アクティブのオブジェクトと無効なコンポーネントを削除します。
        /// </summary>
        /// <param name="instance"></param>
        private static void RemoveInactiveObjectsAndDisabledComponents(GameObject instance)
        {
            foreach (var transform in instance.transform.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform == null || transform.gameObject == null || transform.gameObject.activeSelf)
                {
                    continue;
                }
                Object.DestroyImmediate(transform.gameObject);
            }

            foreach (var component in instance.transform.GetComponentsInChildren<MonoBehaviour>())
            {
                if (component.enabled)
                {
                    continue;
                }
                Object.DestroyImmediate(component);
            }
        }

        private static void SetFirstPersonOffset(GameObject instance)
        {
            var firstPerson = instance.GetComponent<VRMFirstPerson>();
#if VRC_SDK_VRCSDK2
            var avatarDescriptor = instance.GetComponent<VRC_AvatarDescriptor>();
#elif VRC_SDK_VRCSDK3
            var avatarDescriptor = instance.GetComponent<VRCAvatarDescriptor>();
#endif
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            firstPerson.FirstPersonOffset = avatarDescriptor.ViewPosition - firstPerson.FirstPersonBone.position;
#endif
        }

        private static void SetFirstPersonRenderers(GameObject instance)
        {
            instance.GetComponent<VRMFirstPerson>().TraverseRenderers();
        }

        private static void SetLookAtBoneApplyer(GameObject instance)
        {
            var lookAtBoneApplyer = instance.GetComponent<VRMLookAtBoneApplyer>();

            if (VRChatUtility.SDKVersion == 2)
            {
                if (!VRChatUtility.IsEnabledAutoEyeMovementInSDK2(instance))
                {
                    return;
                }

                foreach (var mapper in new[] {
                    lookAtBoneApplyer.HorizontalOuter,
                    lookAtBoneApplyer.HorizontalInner,
                    lookAtBoneApplyer.VerticalDown,
                    lookAtBoneApplyer.VerticalUp,
                })
                {
                    mapper.CurveYRangeDegree = VRChatsBugsWorkaround.MaxAutoEyeMovementDegree;
                }
            }
            else
            {
#if VRC_SDK_VRCSDK3
                var settings = instance.GetComponent<VRCAvatarDescriptor>().customEyeLookSettings;
                if (settings.eyesLookingUp != null && settings.eyesLookingDown != null
                    && settings.eyesLookingLeft != null && settings.eyesLookingRight != null)
                {
                    lookAtBoneApplyer.VerticalUp.CurveYRangeDegree
                        = Math.Min(-settings.eyesLookingUp.left.x, -settings.eyesLookingUp.right.x);
                    lookAtBoneApplyer.VerticalDown.CurveYRangeDegree
                        = Math.Min(settings.eyesLookingDown.left.x, settings.eyesLookingDown.right.x);
                    lookAtBoneApplyer.HorizontalOuter.CurveYRangeDegree
                        = Math.Min(-settings.eyesLookingLeft.left.y, settings.eyesLookingRight.right.y);
                    lookAtBoneApplyer.HorizontalInner.CurveYRangeDegree
                        = Math.Min(-settings.eyesLookingLeft.right.y, settings.eyesLookingRight.left.y);
                }
#endif
            }
        }

    }
}
