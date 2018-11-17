using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// 変換ダイアログ。
    /// </summary>
    public class Wizard : ScriptableWizard
    {
        private static readonly string EditorUserSettingsPrefix = typeof(Wizard).Namespace + ".";

        /// <summary>
        /// 変換後の処理を行うコールバック関数。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="meta"></param>
        public delegate void PostConverting(GameObject avatar, VRMMeta meta);

        /// <summary>
        /// ダイアログの最小サイズ。
        /// </summary>
        private static readonly Vector2 MinSize = new Vector2(x: 800, y: 350);

        /// <summary>
        /// リストにおける一段回分の字下げ幅。
        /// </summary>
        private static readonly int Indent = 20;

        /// <summary>
        /// 複製・変換対象のアバター。
        /// </summary>
        [SerializeField]
        private Animator avatar;

        /// <summary>
        /// アバターの<see cref="VRC_AvatarDescriptor.Animations"/>に設定する値。
        /// </summary>
        [SerializeField]
        private VRC_AvatarDescriptor.AnimationSet defaultAnimationSet;

        /// <summary>
        /// オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool enableEyeMovement;

        /// <summary>
        /// 各種コールバック関数のユーザー設定値。
        /// </summary>
        [SerializeField, Localizable]
        private MonoScript callbackFunctions;

        /// <summary>
        /// <see cref="ComponentsReplacer.SwayingParametersConverter"/>のユーザー設定値。
        /// </summary>
        private ComponentsReplacer.SwayingParametersConverter swayingParametersConverter;

        /// <summary>
        /// <see cref="Wizard.PostConverting"/>のユーザー設定値。
        /// </summary>
        private Wizard.PostConverting postConverting;

        /// <summary>
        /// 変換ダイアログを開きます。
        /// </summary>
        /// <param name="avatar"></param>
        internal static void Open(GameObject avatar)
        {
            var wizard = DisplayWizard<Wizard>(title: "VRM Converter for VRChat " + Converter.Version, createButtonName: Gettext._("Duplicate and Convert"));
            wizard.minSize = Wizard.MinSize;

            wizard.avatar = avatar.GetComponent<Animator>();
        }

        private void Awake()
        {
            string defaultAnimationSet = EditorUserSettings.GetConfigValue(name: Wizard.EditorUserSettingsPrefix + "defaultAnimationSet");
            if (!string.IsNullOrEmpty(defaultAnimationSet))
            {
                this.defaultAnimationSet = (VRC_AvatarDescriptor.AnimationSet)Enum.Parse(enumType: typeof(VRC_AvatarDescriptor.AnimationSet), value: defaultAnimationSet);
            }
            this.enableEyeMovement = string.IsNullOrEmpty(EditorUserSettings.GetConfigValue(name: Wizard.EditorUserSettingsPrefix + "disableEyeMovement"));
            string callbackFunctions = EditorUserSettings.GetConfigValue(name: Wizard.EditorUserSettingsPrefix + "callbackFunctions");
            if (!string.IsNullOrEmpty(callbackFunctions))
            {
                this.callbackFunctions = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath: callbackFunctions);
            }
        }

        private void OnWizardUpdate()
        {
            EditorUserSettings.SetConfigValue(
                name: Wizard.EditorUserSettingsPrefix + "defaultAnimationSet",
                value: this.defaultAnimationSet.ToString()
            );
            EditorUserSettings.SetConfigValue(
                name: Wizard.EditorUserSettingsPrefix + "disableEyeMovement",
                value: this.enableEyeMovement ? "" : "on"
            );
            EditorUserSettings.SetConfigValue(
                name: Wizard.EditorUserSettingsPrefix + "callbackFunctions",
                value: AssetDatabase.GetAssetPath(assetObject: this.callbackFunctions)
            );
        }

        protected override bool DrawWizardGUI()
        {
            base.DrawWizardGUI();
            isValid = true;

            if (this.callbackFunctions)
            {
                Type callBackFunctions = this.callbackFunctions.GetClass();

                this.swayingParametersConverter = Delegate.CreateDelegate(
                    type: typeof(ComponentsReplacer.SwayingParametersConverter),
                    target: callBackFunctions,
                    method: "SwayingParametersConverter",
                    ignoreCase: false,
                    throwOnBindFailure: false
                ) as ComponentsReplacer.SwayingParametersConverter;

                this.postConverting = Delegate.CreateDelegate(
                    type: typeof(Wizard.PostConverting),
                    target: callBackFunctions,
                    method: "PostConverting",
                    ignoreCase: false,
                    throwOnBindFailure: false
                ) as Wizard.PostConverting;
            }

            var indentStyle = new GUIStyle() { padding = new RectOffset() { left = Wizard.Indent } };
            EditorGUILayout.LabelField(
                label: (this.swayingParametersConverter != null ? "☑" : "☐")
                    + " public static void SwayingParametersConverter(DynamicBoneParameters, SpringBoneParameters, BoneInfo)",
                style: indentStyle
            );
            
            EditorGUILayout.LabelField(
                label: (this.postConverting != null ? "☑" : "☐")
                    + " public static void PostConverting(GameObject, VRMMeta)",
                style: indentStyle
            );

            foreach (var type in Converter.RequiredComponents) {
                if (!this.avatar.GetComponent(type: type))
                {
                    EditorGUILayout.HelpBox(string.Format(Gettext._("Not set “{0}” component."), type), MessageType.Error);
                    isValid = false;
                }
            }

            if (Application.unityVersion != VRChatUtility.SupportedUnityVersion)
            {
                EditorGUILayout.HelpBox(string.Format(
                    "Unity {0} is running. If you are using a different version than {1}, VRCSDK might not work correctly. Recommended using Unity downloaded from {2} .",
                    Application.unityVersion,
                    VRChatUtility.SupportedUnityVersion,
                    VRChatUtility.DownloadURL
                ), MessageType.Warning);
            }

            return true;
        }

        private void OnWizardCreate()
        {
            var avatar = Instantiate(this.avatar.gameObject) as GameObject;

            IEnumerable<Converter.Message> messages = Converter.Convert(
                avatar: avatar,
                defaultAnimationSet: this.defaultAnimationSet,
                swayingParametersConverter: this.swayingParametersConverter,
                assetsPath: GetAssetsPath(vrm: this.avatar.gameObject),
                enableAutoEyeMovement: this.enableEyeMovement
            );

            if (this.postConverting != null) {
                this.postConverting(avatar, this.avatar.gameObject.GetComponent<VRMMeta>());
            }

            ResultDialog.Open(messages: messages);
        }

        /// <summary>
        /// プレハブのパスを取得します。
        /// </summary>
        /// <param name="vrm"></param>
        /// <returns></returns>
        private string GetAssetsPath(GameObject vrm)
        {
            var path = UnityPath.FromAsset(asset: vrm);
            if (!path.IsUnderAssetsFolder)
            {
                var prefab = PrefabUtility.GetPrefabParent(vrm);
                if (prefab)
                {
                    path = UnityPath.FromAsset(asset: prefab);
                }
                else
                {
                    path = UnityPath.FromUnityPath("Assets/" + avatar.name);
                }
            }

            return path.Value;
        }
    }
}