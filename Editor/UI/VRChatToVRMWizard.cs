using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;
using SkinnedMeshUtility = Esperecyan.Unity.VRMConverterForVRChat.Utilities.SkinnedMeshUtility;
using Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM;

namespace Esperecyan.Unity.VRMConverterForVRChat.UI
{
    /// <summary>
    /// 変換ダイアログ。
    /// </summary>
    internal class VRChatToVRMWizard : ScriptableWizard
    {
        private static readonly IDictionary<ExpressionPreset, string> PresetFieldPairs
            = new Dictionary<ExpressionPreset, string>()
            {
                { ExpressionPreset.Happy, nameof(VRChatExpressionBinding.AnimationClip) },
                { ExpressionPreset.Angry, nameof(VRChatExpressionBinding.AnimationClip) },
                { ExpressionPreset.Sad, nameof(VRChatExpressionBinding.AnimationClip) },
                { ExpressionPreset.Relaxed, nameof(VRChatExpressionBinding.AnimationClip) },
                { ExpressionPreset.Surprised, nameof(VRChatExpressionBinding.AnimationClip) },
                { ExpressionPreset.Blink, nameof(VRChatExpressionBinding.ShapeKeyNames) },
                { ExpressionPreset.BlinkLeft, nameof(VRChatExpressionBinding.ShapeKeyNames) },
                { ExpressionPreset.BlinkRight, nameof(VRChatExpressionBinding.ShapeKeyNames) },
            };

        private static int UnityEditorMaxMultiSelectCount = 32;

        /// <summary>
        /// エクスポート対象のVRChatアバター (Humanoid)。
        /// </summary>
        private GameObject prefabOrInstance = null;

        private VRMMetaObject meta = null;

        private IEnumerable<string> shapeKeyNames = null;
        private string[] maybeBlinkShapeKeyNames = null;
        private IEnumerable<AnimationClip> animations = null;
        private string[] animationNames = null;
        private IDictionary<ExpressionPreset, VRChatExpressionBinding> expressions = null;
        private IDictionary<ExpressionPreset, int> expressionPresetFlagPairs = null;
        private Editor metaEditor = null;

        /// <summary>
        /// 設定ダイアログを開きます。
        /// </summary>
        /// <param name="prefabOrInstance"></param>
        internal static void Open(GameObject prefabOrInstance)
        {
            var wizard = ScriptableWizard.DisplayWizard<VRChatToVRMWizard>(
                title: _("VRM Settings") + $" | {Converter.Name} {Converter.Version}",
                createButtonName: _("Export VRM file")
            );

            wizard.prefabOrInstance = prefabOrInstance;
            wizard.meta = ScriptableObject.CreateInstance<VRMMetaObject>();
        }

        private static IDictionary<T, int> ToKeyFlagPairs<T>(IEnumerable<T> keys)
        {
            return keys
                .Take(VRChatToVRMWizard.UnityEditorMaxMultiSelectCount)
                .Select((key, index) => (key, index))
                .ToDictionary(
                    keyIndexPairs => keyIndexPairs.key,
                    keyIndexPairs => 1 << keyIndexPairs.index
                );
        }

        private static int ToFlags<T>(IEnumerable<T> allKeys, IEnumerable<T> keys)
        {
            var keyFlagPairs = VRChatToVRMWizard.ToKeyFlagPairs(allKeys);
            var flags = 0;
            foreach (var key in keys)
            {
                if (!keyFlagPairs.ContainsKey(key))
                {
                    // UnityEditorMaxMultiSelectCountオーバー
                    continue;
                }
                flags |= keyFlagPairs[key];
            }
            return flags;
        }

        private static IEnumerable<T> ToKeys<T>(IEnumerable<T> allKeys, int flags)
        {
            var keyFlagPairs = VRChatToVRMWizard.ToKeyFlagPairs(allKeys);
            var keys = new List<T>();
            foreach (var (key, flag) in keyFlagPairs)
            {
                if ((flags & flag) != 0)
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        /// <summary>
        /// <see cref="Wizard"/>の保存するフィールド一覧を取得します。
        /// </summary>
        /// <returns></returns>
        private IEnumerable<FieldInfo> GetSavedFieldInfos()
        {
            return this.GetType().GetFields(bindingAttr: BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(info => info.Name == "destinationPath"
                    || info.Name != "avatar" && info.GetCustomAttributes(attributeType: typeof(SerializeField), inherit: false).Length > 0);
        }

        protected override bool DrawWizardGUI()
        {
            this.isValid = true;

            if (VRChatUtility.SDKVersion == null)
            {
                EditorGUILayout.HelpBox(_("VRChat SDK2 or SDK3 has not been imported."), MessageType.Error);
                this.isValid = false;
                return true;
            }

            if (!this.prefabOrInstance.GetComponent<Animator>().isHuman)
            {
                EditorGUILayout.HelpBox(_("This is not humanoid."), MessageType.Error);
                this.isValid = false;
                return true;
            }

            if (Application.unityVersion != VRChatUtility.SDKSupportedUnityVersion)
            {
                EditorGUILayout.HelpBox(string.Format(
                    _("Unity {0} is running. If you are using a different version than {1}, VRChat SDK might not work correctly. Recommended using Unity downloaded from {2} ."),
                    Application.unityVersion,
                    VRChatUtility.SDKSupportedUnityVersion,
                    VRChatUtility.DownloadURL
                ), MessageType.Warning);
            }

            if (this.expressions == null)
            {
                this.shapeKeyNames = this.prefabOrInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Select(renderer => renderer.sharedMesh)
                    .Where(mesh => mesh != null)
                    .SelectMany(mesh => SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents: false))
                    .Select(shapeKey => shapeKey.Name)
                    .Distinct();

                (this.animations, this.expressions)
                    = VRChatUtility.DetectVRChatExpressions(this.prefabOrInstance, this.shapeKeyNames);
                this.animationNames = new[] { "-" }.Concat(this.animations.Select(animation => animation.name)).ToArray();

                this.maybeBlinkShapeKeyNames = this.expressions
                    .Where(expression => VRChatToVRMWizard.PresetFieldPairs.ContainsKey(expression.Key)
                        && VRChatToVRMWizard.PresetFieldPairs[expression.Key] == nameof(VRChatExpressionBinding.ShapeKeyNames))
                    .SelectMany(expression => expression.Value.ShapeKeyNames)
                    .Concat(VRChatUtility.DetectBlinkShapeKeyNames(this.shapeKeyNames))
                    .Distinct()
                    .Take(VRChatToVRMWizard.UnityEditorMaxMultiSelectCount)
                    .ToArray();

                this.expressionPresetFlagPairs = VRChatToVRMWizard.PresetFieldPairs
                    .ToDictionary(
                        presetFieldPair => presetFieldPair.Key,
                        presetFieldPair => this.expressions.ContainsKey(presetFieldPair.Key)
                            ? (VRChatToVRMWizard.PresetFieldPairs[presetFieldPair.Key] == nameof(VRChatExpressionBinding.AnimationClip)
                                ? 1 + this.animations.ToList().IndexOf(this.expressions[presetFieldPair.Key].AnimationClip)
                                : VRChatToVRMWizard.ToFlags(this.maybeBlinkShapeKeyNames, this.expressions[presetFieldPair.Key].ShapeKeyNames))
                            : 0
                    );

                this.metaEditor = Editor.CreateEditor(this.meta);
            }

            EditorGUILayout.LabelField("Expressions", EditorStyles.boldLabel);
            foreach (var (preset, field) in VRChatToVRMWizard.PresetFieldPairs)
            {
                this.expressionPresetFlagPairs[preset]
                    = VRChatToVRMWizard.PresetFieldPairs[preset] == nameof(VRChatExpressionBinding.AnimationClip)
                        ? EditorGUILayout.Popup(
                            preset.ToString(),
                            this.expressionPresetFlagPairs[preset],
                            this.animationNames
                        )
                        : EditorGUILayout.MaskField(
                            preset.ToString(),
                            this.expressionPresetFlagPairs[preset],
                            this.maybeBlinkShapeKeyNames
                        );
            }

            this.metaEditor.OnInspectorGUI();

            return true;
        }

        private void OnWizardCreate()
        {
            var path = EditorUtility.SaveFilePanel(
                title: _("Export VRM file"),
                directory: "",
                defaultName: "test.vrm",
                extension: "vrm"
            );
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            this.expressions = this.expressionPresetFlagPairs
                .Where(expression => expression.Value != 0)
                .ToDictionary(
                    expression => expression.Key,
                    expression => VRChatToVRMWizard.PresetFieldPairs[expression.Key] == nameof(VRChatExpressionBinding.AnimationClip)
                        ? new VRChatExpressionBinding() { AnimationClip = this.animations.ElementAt(expression.Value - 1) }
                        : new VRChatExpressionBinding() { ShapeKeyNames = VRChatToVRMWizard.ToKeys(this.maybeBlinkShapeKeyNames, expression.Value) }
                ).Concat(this.expressions.Where(expression => VRChatUtility.ExpressionPresetVRChatVisemeIndexPairs.Keys.Contains(expression.Key)))
                .ToDictionary(expression => expression.Key, expression => expression.Value);

            if (string.IsNullOrEmpty(this.meta.Title))
            {
                this.meta.Title = this.prefabOrInstance.name;
            }
            if (this.meta.Version == null)
            {
                this.meta.Version = "";
            }
            if (this.meta.Author == null)
            {
                this.meta.Author = " ";
            }
            if (this.meta.ContactInformation == null)
            {
                this.meta.ContactInformation = "";
            }
            if (this.meta.Reference == null)
            {
                this.meta.Reference = "";
            }

            VRChatToVRMConverter.Convert(
                path,
                this.prefabOrInstance,
                this.meta,
                this.expressions,
                VRChatToVRMConverter.DefaultSwayingParametersConverter
            );

            EditorUtility.DisplayDialog(Converter.Name + " " + Converter.Version, $"「{path}」へ出力が完了しました。", "OK");
        }
    }
}
