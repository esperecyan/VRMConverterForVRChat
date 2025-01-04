#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UniGLTF;
using UniGLTF.MeshUtility;
using VRM;
using Esperecyan.UniVRMExtensions;
using Esperecyan.UniVRMExtensions.SwayingObjects;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using SkinnedMeshUtility = Esperecyan.Unity.VRMConverterForVRChat.Utilities.SkinnedMeshUtility;
using Esperecyan.Unity.VRMConverterForVRChat.UI;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

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
        /// VRChatアバターインスタンスからVRMインスタンスへ変換します。
        /// </summary>
        /// <param name="version">当エディタ拡張のバージョン。</param>
        /// <param name="outputPath"></param>
        /// <param name="instance">ヒエラルキー上のGameObject。</param>
        /// <param name="presetVRChatBindingPairs">各表情への割り当て。</param>
        /// <param name="meta"></param>
        /// <param name="presetVRChatBindingPairs"></param>
        /// <param name="keepUnusedShapeKeys"></param>
        internal static void Convert(
            string version,
            string outputPath,
            GameObject instance,
            VRMMetaObject meta,
            IDictionary<ExpressionPreset, VRChatExpressionBinding> presetVRChatBindingPairs,
            bool keepUnusedShapeKeys
        )
        {
            GameObject? clone = null;
            try
            {
                var rootObjectName = instance.name;
                clone = Object.Instantiate(instance);

                // 非表示のオブジェクト・コンポーネントを削除
                // TODO: アクティブ・非アクティブの切り替えをシェイプキーに変換する
                VRChatToVRMConverter.RemoveInactiveObjectsAndDisabledComponents(clone);


                // 表情とシェイプキー名の組み合わせを取得
                var presetShapeKeyNameWeightPairsPairs = presetVRChatBindingPairs.ToDictionary(
                    presetVRChatBindingPair => presetVRChatBindingPair.Key,
                    presetVRChatBindingPair => VRChatExpressionsReplacer.ExtractShapeKeyNames(presetVRChatBindingPair.Value)
                );

                // VRM設定1
                var temporaryFolder = UnityPath.FromUnityPath(VRChatToVRMConverter.TemporaryFolderPath);
                temporaryFolder.EnsureFolder();
                var temporaryPrefabPath = temporaryFolder.Child(VRChatToVRMConverter.TemporaryPrefabFileName).Value;
                VRMInitializer.Initialize(temporaryPrefabPath, clone);
                Object.DestroyImmediate(clone.GetComponentInChildren<VRMSpringBone>()); // VRMInitializer.Initialize() によってアタッチされた空のVRMSpringBoneを削除
                VRChatToVRMConverter.SetFirstPersonOffset(clone);
                VRChatToVRMConverter.SetLookAtBoneApplyer(clone);
                var sourceAndDestination = clone.GetComponent<Animator>();
                if (sourceAndDestination.GetComponentInChildren<VRCPhysBone>() != null)
                {
                    // VRCPhysBoneが含まれていれば
                    VRCPhysBonesToVRMSpringBonesConverter.Convert(
                        source: sourceAndDestination,
                        destination: sourceAndDestination
                    );
                }
                else if (DynamicBones.IsImported())
                {
                    DynamicBonesToVRMSpringBonesConverter.Convert(
                        source: sourceAndDestination,
                        destination: sourceAndDestination
                    );
                }
                VRChatToVRMConverter.RemoveUnusedColliderGroups(clone);

                // 正規化
                VRMBoneNormalizer.Execute(clone, forceTPose: true, useCurrentBlendShapeWeight: true);

                // 全メッシュ結合
                var combinedRenderer = CombineMeshesAndSubMeshes.Combine(
                    clone,
                    notCombineRendererObjectNames: new List<string>(),
                    destinationObjectName: "vrm-mesh",
                    savingAsAsset: false
                );

                if (!keepUnusedShapeKeys)
                {
                    // 使用していないシェイプキーの削除
                    SkinnedMeshUtility.CleanUpShapeKeys(combinedRenderer.sharedMesh, presetShapeKeyNameWeightPairsPairs
                        .SelectMany(presetShapeKeyNameWeightPairsPair => presetShapeKeyNameWeightPairsPair.Value.Keys)
                        .Distinct());
                }

                // シェイプキーの分離
                TabMeshSeparator.SeparationProcessing(clone);

                // マテリアルの設定・アセットとして保存
                VRChatToVRMConverter.ReplaceShaders(clone, temporaryPrefabPath);

                // GameObject・メッシュなどをアセットとして保存 (アセットとして存在しないと正常にエクスポートできない)
                clone.name = rootObjectName;
                var animator = clone.GetComponent<Animator>();
                animator.avatar = Duplicator.CreateObjectToFolder(animator.avatar, temporaryPrefabPath);
                meta.name = "Meta";
                clone.GetComponent<VRMMeta>().Meta = Duplicator.CreateObjectToFolder(meta, temporaryPrefabPath);
                foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMesh.name = renderer.name;
                    renderer.sharedMesh = Duplicator.CreateObjectToFolder(renderer.sharedMesh, temporaryPrefabPath);
                }

                // VRM設定2
                VRChatToVRMConverter.SetFirstPersonRenderers(clone);

                // 表情の設定
                VRChatExpressionsReplacer.SetExpressions(clone, presetShapeKeyNameWeightPairsPairs);

                var prefab = PrefabUtility
                    .SaveAsPrefabAssetAndConnect(clone, temporaryPrefabPath, InteractionMode.AutomatedAction);

                // エクスポート
                AssetDatabase.SaveAssets();
                File.WriteAllBytes(
                    outputPath,
                    VRMEditorExporter.Export(prefab, meta: null, ScriptableObject.CreateInstance<VRMExportSettings>())
                );
            }
            catch (Exception exception)
            {
                ErrorDialog.Open(version, exception);
                throw;
            }
            finally
            {
                if (clone != null)
                {
                    Object.DestroyImmediate(clone);
                }
                AssetDatabase.DeleteAsset("Assets/VRMConverterTemporary");
            }
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
                if (component == null || component.enabled)
                {
                    continue;
                }
                Object.DestroyImmediate(component);
            }
        }

        private static void SetFirstPersonOffset(GameObject instance)
        {
            var avatarDescriptor = instance.GetComponent<VRC_AvatarDescriptor>();
            var firstPerson = instance.GetComponent<VRMFirstPerson>();
            firstPerson.FirstPersonOffset = avatarDescriptor.ViewPosition - firstPerson.FirstPersonBone.position;
        }

        private static void SetFirstPersonRenderers(GameObject instance)
        {
            instance.GetComponent<VRMFirstPerson>().TraverseRenderers();
        }

        private static void SetLookAtBoneApplyer(GameObject instance)
        {
            var lookAtBoneApplyer = instance.GetComponent<VRMLookAtBoneApplyer>();

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

    }
}
