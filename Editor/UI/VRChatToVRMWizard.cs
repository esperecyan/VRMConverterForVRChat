#nullable enable
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using VRC.SDKBase;
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

        private static readonly int UnityEditorMaxMultiSelectCount = 32;

        private string version = null!;

        /// <summary>
        /// エクスポート対象のVRChatアバター (Humanoid)。
        /// </summary>
        private GameObject? prefabOrInstance = null;

        private VRMMetaObject meta = null!;

        private IEnumerable<string>? shapeKeyNames = null;
        private bool noShapeKeys = true;
        private string[]? maybeBlinkShapeKeyNames = null;
        private IEnumerable<AnimationClip>? animations = null;
        private string[]? animationNames = null;
        private IDictionary<ExpressionPreset, VRChatExpressionBinding>? expressions = null;
        private IDictionary<ExpressionPreset, int>? expressionPresetFlagPairs = null;
        private bool keepUnusedShapeKeys = false;
        private Editor? metaEditor = null;


        private enum ExpressionBindingType
        {
            None,
            AnimationClip,
            ShapeKeys
        }

        private class CustomExpressionInfo
        {
            public ExpressionPreset Preset;
            public ExpressionBindingType BindingType;
            public int AnimationIndex;
            public int ShapeKeyFlags;

            public CustomExpressionInfo(string name)
            {
                Preset = ExpressionPreset.CreateCustom(name);
                BindingType = ExpressionBindingType.None;
                AnimationIndex = 0;
                ShapeKeyFlags = 0;
            }
        }

        private List<CustomExpressionInfo> customExpressions = new List<CustomExpressionInfo>();

        private Dictionary<ExpressionPreset, int> customExpressionAnimationIndices =
            new Dictionary<ExpressionPreset, int>();

        /// <summary>
        /// 設定ダイアログを開きます。
        /// </summary>
        /// <param name="prefabOrInstance"></param>
        internal static async void Open(GameObject prefabOrInstance)
        {
            var version = await Converter.GetVersion();
            var wizard = ScriptableWizard.DisplayWizard<VRChatToVRMWizard>(
                title: _("VRM Settings") + $" | {Converter.Name} {version}",
                createButtonName: _("Export VRM file")
            );
            wizard.version = version;
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

            if (this.prefabOrInstance == null)
            {
                // 選択されたオブジェクトが削除されていれば
                this.Close();
                return true;
            }

            var animator = this.prefabOrInstance.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                EditorGUILayout.HelpBox(_("This is not humanoid."), MessageType.Error);
                this.isValid = false;
                return true;
            }

            if (this.prefabOrInstance.GetComponent<VRC_AvatarDescriptor>() == null)
            {
                EditorGUILayout.HelpBox(
                    string.Format(_("Not set “{0}” component."), "VRCAvatarDescriptor"),
                    MessageType.Error
                );
                this.isValid = false;
                return true;
            }

            if (this.expressions == null || this.expressionPresetFlagPairs == null || this.metaEditor == null)
            {
                this.shapeKeyNames = this.prefabOrInstance.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Select(renderer => renderer.sharedMesh)
                    .Where(mesh => mesh != null)
                    .SelectMany(mesh => SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents: false))
                    .Select(shapeKey => shapeKey.Name)
                    .Distinct();
                this.noShapeKeys = this.shapeKeyNames.Count() == 0;

                (this.animations, this.expressions)
                    = VRChatUtility.DetectVRChatExpressions(this.prefabOrInstance, this.shapeKeyNames);
                this.animationNames = new[] { "-" }.Concat(this.animations.Select(animation => animation.name)).ToArray();

                var maybeBlinkShapeKeyNames = this.expressions
                    .Where(expression => VRChatToVRMWizard.PresetFieldPairs.ContainsKey(expression.Key)
                        && VRChatToVRMWizard.PresetFieldPairs[expression.Key] == nameof(VRChatExpressionBinding.ShapeKeyNames))
                    .SelectMany(expression => expression.Value.ShapeKeyNames)
                    .Concat(VRChatUtility.DetectBlinkShapeKeyNames(this.shapeKeyNames))
                    .Distinct();
                if (maybeBlinkShapeKeyNames.Count() == 0)
                {
                    // まばたきシェイプキーが見つからなかった場合
                    maybeBlinkShapeKeyNames = this.shapeKeyNames;
                }
                this.maybeBlinkShapeKeyNames = maybeBlinkShapeKeyNames
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

            // 空のマテリアルスロットがあれば「Export VRM file」ボタンを押せないように
            var missingMaterialPaths = new List<string>();
            foreach (var renderer in this.prefabOrInstance.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        continue;
                    }

                    missingMaterialPaths
                        .Add($"{renderer.transform.RelativePathFrom(this.prefabOrInstance.transform)}[{i}]");
                }
            }
            if (missingMaterialPaths.Count > 0)
            {
                EditorGUILayout.HelpBox(_("The below material slots are none") + ":\n"
                    + string.Join("\n", missingMaterialPaths.Select(path => "• " + path)), MessageType.Error);
                this.isValid = false;
            }

            using (new EditorGUI.DisabledScope(this.noShapeKeys))
            {
                EditorGUILayout.LabelField("Expressions", EditorStyles.boldLabel);
                foreach (var (preset, field) in VRChatToVRMWizard.PresetFieldPairs)
                {
                    if (this.noShapeKeys)
                    {
                        // アバターにシェイプキーが1つも含まれていない場合
                        EditorGUILayout.Popup(preset.Name, 0, new string[] { });
                        continue;
                    }

                    if (VRChatToVRMWizard.PresetFieldPairs[preset] == nameof(VRChatExpressionBinding.AnimationClip))
                    {
                        this.expressionPresetFlagPairs[preset] = EditorGUILayout.Popup(
                            preset.Name,
                            this.expressionPresetFlagPairs[preset],
                            this.animationNames
                        );
                    }
                    else
                    {
                        // まばたき
                        this.expressionPresetFlagPairs[preset] = EditorGUILayout.MaskField(
                            preset.Name,
                            this.expressionPresetFlagPairs[preset],
                            this.maybeBlinkShapeKeyNames
                        );
                    }
                }

                // カスタム表情の設定
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Custom Expressions", EditorStyles.boldLabel);
                for (int i = 0; i < customExpressions.Count; i++)
                {
                    var expression = customExpressions[i];
                    EditorGUILayout.BeginHorizontal();

                    // 表情名の入力フィールド
                    string newName = EditorGUILayout.TextField(expression.Preset.Name);
                    if (newName != expression.Preset.Name)
                    {
                        expression.Preset = ExpressionPreset.CreateCustom(newName);
                    }

                    // バインディングタイプの選択
                    expression.BindingType = (ExpressionBindingType)EditorGUILayout.EnumPopup(expression.BindingType);

                    // バインディングタイプに応じた設定
                    switch (expression.BindingType)
                    {
                        case ExpressionBindingType.AnimationClip:
                            expression.AnimationIndex = EditorGUILayout.Popup(expression.AnimationIndex, this.animationNames);
                            break;
                        case ExpressionBindingType.ShapeKeys:
                            expression.ShapeKeyFlags = EditorGUILayout.MaskField(expression.ShapeKeyFlags, this.maybeBlinkShapeKeyNames);
                            break;
                    }

                    // 削除ボタン
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        customExpressions.RemoveAt(i);
                        i--;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add Custom Expression"))
                {
                    customExpressions.Add(new CustomExpressionInfo("New Expression"));
                }
            }

            EditorGUILayout.LabelField("Other Settings", EditorStyles.boldLabel);
            this.keepUnusedShapeKeys = EditorGUILayout.Toggle(_("Keep unused shape keys"), this.keepUnusedShapeKeys);

            this.metaEditor.OnInspectorGUI();

            return true;
        }

        private void OnWizardCreate()
        {
            if (this.prefabOrInstance == null
                || this.maybeBlinkShapeKeyNames == null)
            {
                throw new InvalidOperationException();
            }

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
                )
                .Concat(this.expressions.Where(expression => VRChatUtility.ExpressionPresetVRChatVisemeIndexPairs.Keys.Contains(expression.Key)))
                .Concat(this.customExpressions.Select(customExpression =>
                    new KeyValuePair<ExpressionPreset, VRChatExpressionBinding>(
                        customExpression.Preset,
                        new VRChatExpressionBinding() {
                            AnimationClip = customExpression.BindingType == ExpressionBindingType.AnimationClip
                                && this.animations != null
                                && customExpression.AnimationIndex > 0
                                && customExpression.AnimationIndex <= this.animations.Count()
                                    ? this.animations.ElementAt(customExpression.AnimationIndex - 1)
                                    : null,
                            ShapeKeyNames = customExpression.BindingType == ExpressionBindingType.ShapeKeys
                                && this.maybeBlinkShapeKeyNames != null
                                    ? VRChatToVRMWizard.ToKeys(this.maybeBlinkShapeKeyNames, customExpression.ShapeKeyFlags)
                                    : null
                        }
                    )
                ))
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
                this.version,
                path,
                this.prefabOrInstance,
                this.meta,
                this.expressions,
                this.keepUnusedShapeKeys
            );

            EditorUtility.DisplayDialog(Converter.Name + " " + this.version, $"「{path}」へ出力が完了しました。", "OK");
        }
    }
}
