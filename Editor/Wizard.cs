using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.IO;
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
        private static readonly string EditorUserSettingsName = typeof(Wizard).Namespace;
        private static readonly string EditorUserSettingsXmlNamespace = "https://pokemori.booth.pm/items/1025226";

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
        /// オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool enableEyeMovement = true;

        /// <summary> 
        /// オートアイムーブメント有効化時、目ボーンのPositionのZに加算する値。 
        /// </summary> 
        [SerializeField, Localizable(0, 0.1f)]
        private float moveEyeBoneToFrontForEyeMovement;

        /// <summary>
        /// VRChat上でモデルがなで肩・いかり肩になる問題について、ボーンのPositionのYに加算する値。
        /// </summary>
        [SerializeField, Localizable(-0.1f, 0.1f)]
        private float shoulderHeights;

        /// <summary>
        /// 伏せたときのアバターの位置が、自分視点と他者視点で異なるVRChatのバグに対処するなら <c>true</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool fixProneAvatarPosition = true;

        /// <summary>
        /// 結合しないメッシュレンダラーのオブジェクト名。
        /// </summary>
        [SerializeField, Localizable]
        private List<string> notCombineRendererObjectNames = new List<string>();

        [Header("For PC")]

        /// <summary>
        /// 揺れ物を変換するか否かの設定。
        /// </summary>
        [SerializeField, Localizable]
        private ComponentsReplacer.SwayingObjectsConverterSetting swayingObjects;

        /// <summary>
        /// 揺れ物のパラメータを引き継ぐなら<c>true</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool takeOverSwayingParameters = true;

        /// <summary>
        /// 除外する揺れ物の<see cref="VRMSpringBone.m_comment" />。
        /// </summary>
        [SerializeField, Localizable]
        private List<string> excludedSpringBoneComments = new List<string>();

        [Header("For Quest")]

        /// <summary>
        /// Quest向けに変換するなら <c>true</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool forQuest = false;

        [Header("Callback")]

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
        /// 「Assets/」で始まり「.prefab」で終わる保存先のパス。
        /// </summary>
        private string destinationPath;

        /// <summary>
        /// 変換ダイアログを開きます。
        /// </summary>
        /// <param name="avatar"></param>
        internal static void Open(GameObject avatar)
        {
            var wizard = DisplayWizard<Wizard>(title: Converter.Name + " " + Converter.Version, createButtonName: Gettext._("Duplicate and Convert"));
            wizard.minSize = Wizard.MinSize;

            wizard.avatar = avatar.GetComponent<Animator>();

            wizard.LoadSettings();
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

        /// <summary>
        /// アバターごとの変換設定を記録したXML文書を返します。
        /// </summary>
        /// <returns>まだ何も保存されていない場合、またはXMLパースエラーが発生した場合は、ルート要素のみ存在する文書を返します。</returns>
        private XmlDocument GetSettingsList()
        {
            var defaultDocument = new XmlDocument();
            defaultDocument.AppendChild(defaultDocument.CreateElement(qualifiedName: "list", namespaceURI: Wizard.EditorUserSettingsXmlNamespace));

            string configValue = EditorUserSettings.GetConfigValue(Wizard.EditorUserSettingsName);
            if (string.IsNullOrEmpty(configValue))
            {
                return defaultDocument;
            }

            var document = new XmlDocument();
            try
            {
                document.LoadXml(xml: configValue);
            }
            catch (XmlException)
            {
                return defaultDocument;
            }

            return document;
        }

        /// <summary>
        /// XML文書から指定したタイトルのアバターの変換設定を取得します。
        /// </summary>
        /// <param name="document"></param>
        /// <param name="title"></param>
        /// <returns>存在しない場合、ルート要素へ属性が設定されていないsettings要素を追加し、それを返します。</returns>
        private XmlElement GetSettings(XmlDocument document, string title)
        {
            XmlElement settings = document.GetElementsByTagName(localName: "settings", namespaceURI: Wizard.EditorUserSettingsXmlNamespace)
                .Cast<XmlElement>().FirstOrDefault(predicate: element => element.GetAttribute(name: "title") == title);
            if (settings != null) {
                return settings;
            }

            settings = document.CreateElement(qualifiedName: "settings", namespaceURI: Wizard.EditorUserSettingsXmlNamespace);
            document.DocumentElement.AppendChild(settings);
            return settings;
        }
        
        /// <summary>
        /// 選択されているアバターの変換設定を反映します。
        /// </summary>
        private void LoadSettings()
        {
            string title = this.avatar.GetComponent<VRMMeta>().Meta.Title;
            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            XmlElement settings = this.GetSettings(document: this.GetSettingsList(), title: title);
            if (string.IsNullOrEmpty(settings.GetAttribute("title")))
            {
                return;
            }

            foreach (FieldInfo info in this.GetSavedFieldInfos())
            {
                Type type = info.FieldType;

                object fieldValue = null;
                if (type == typeof(List<string>))
                {
                    XmlElement list = settings.GetElementsByTagName(localName: info.Name, namespaceURI: Wizard.EditorUserSettingsXmlNamespace)
                        .Cast<XmlElement>().FirstOrDefault();
                    if (list == null)
                    {
                        continue;
                    }
                    fieldValue = list.ChildNodes.Cast<XmlElement>().Select(element => element.InnerText).ToList();
                }
                else
                {
                    string value = settings.GetAttribute(info.Name);
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (typeof(Enum).IsAssignableFrom(type))
                    {
                        fieldValue = Enum.Parse(enumType: type, value: value);
                    }
                    else if (type == typeof(bool))
                    {
                        fieldValue = bool.Parse(value);
                    }
                    else if (type == typeof(float))
                    {
                        fieldValue = float.Parse(value);
                    }
                    else if (type == typeof(string))
                    {
                        fieldValue = value;
                    }
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    {
                        fieldValue = AssetDatabase.LoadAssetAtPath(value, type);
                    }
                }
                info.SetValue(obj: this, value: fieldValue);
            }
        }

        /// <summary>
        /// 選択されているアバターの変換設定を保存します。
        /// </summary>
        private void SaveSettings()
        {
            string title = this.avatar.GetComponent<VRMMeta>().Meta.Title;
            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            XmlDocument document = this.GetSettingsList();
            XmlElement settings = this.GetSettings(document: document, title: title);
            settings.SetAttribute("title", title);
            
            foreach (FieldInfo info in this.GetSavedFieldInfos())
            {
                Type type = info.FieldType;
                object fieldValue = info.GetValue(obj: this);
                string value = "";
                if (typeof(Enum).IsAssignableFrom(type) || type == typeof(bool) || type == typeof(float) || type == typeof(string))
                {
                    value = fieldValue.ToString();
                }
                else if (type == typeof(List<string>))
                {
                    var list = settings.GetElementsByTagName(
                        localName: info.Name,
                        namespaceURI: Wizard.EditorUserSettingsXmlNamespace
                    ).Cast<XmlElement>().FirstOrDefault();
                    if (list != null)
                    {
                        list.RemoveAll();
                    }
                    else
                    {
                        list = document.CreateElement(
                            qualifiedName: info.Name,
                            namespaceURI: Wizard.EditorUserSettingsXmlNamespace
                        );
                    }
                    foreach (var content in fieldValue as List<string>)
                    {
                        if (string.IsNullOrEmpty(content)) {
                            continue;
                        }
                        XmlElement element = document.CreateElement(
                            qualifiedName: "element",
                            namespaceURI: Wizard.EditorUserSettingsXmlNamespace
                        );
                        element.InnerText = content;
                        list.AppendChild(element);
                    }
                    settings.AppendChild(list);
                    continue;
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    value = AssetDatabase.GetAssetPath(fieldValue as UnityEngine.Object);
                }

                settings.SetAttribute(info.Name, value);
            }

            var writer = new Writer();
            document.Save(writer: writer);
            EditorUserSettings.SetConfigValue(Wizard.EditorUserSettingsName, writer.ToString());
            writer.Close();
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
                    + " public static DynamicBoneParameters SwayingParametersConverter(SpringBoneParameters, BoneInfo)",
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

            IEnumerable<string> excludedSpringBoneComments = this.excludedSpringBoneComments.Except(new[] { "" });
            if (excludedSpringBoneComments.Count() > 0)
            {
                IEnumerable<string> comments = excludedSpringBoneComments.Except(
                    this.GetSpringBonesWithComments(prefab: this.avatar.gameObject, comments: excludedSpringBoneComments)
                        .Select(commentAndSpringBones => commentAndSpringBones.Key)
                );
                if (comments.Count() > 0)
                {
                    EditorGUILayout.HelpBox(string.Join(separator: "\n• ", value: new[] { Gettext._("VRMSpringBones with the below Comments do not exist.") }
                        .Concat(comments).ToArray()), MessageType.Warning);
                }
            }

            IEnumerable<string> notCombineRendererObjectNames = this.notCombineRendererObjectNames.Except(new[] { "" });
            if (notCombineRendererObjectNames.Count() > 0)
            {
                IEnumerable<string> names = notCombineRendererObjectNames.Except(
                    this.avatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Concat<Component>(this.avatar.GetComponentsInChildren<MeshRenderer>())
                        .Select(renderer => renderer.name)
                );
                if (names.Count() > 0)
                {
                    EditorGUILayout.HelpBox(string.Join(separator: "\n• ", value: new[] { Gettext._("Renderers on the below name GameObject do not exist.") }
                        .Concat(names).ToArray()), MessageType.Warning);
                }
            }

            string version = VRChatUtility.GetSupportedUnityVersion();
            if (version != "" && Application.unityVersion != version)
            {
                EditorGUILayout.HelpBox(string.Format(
                    Gettext._("Unity {0} is running. If you are using a different version than {1}, VRChat SDK might not work correctly. Recommended using Unity downloaded from {2} ."),
                    Application.unityVersion,
                    version,
                    VRChatUtility.DownloadURL
                ), MessageType.Warning);
            }

            if (!isValid)
            {
                return true;
            }

            AvatarPerformanceStats statistics = AvatarPerformance.CalculatePerformanceStats(
                avatarName: avatar.GetComponent<VRMMeta>().Meta.Title,
                avatarObject: this.avatar.gameObject
            );

            AvatarPerformanceStatsLevel badPerformanceStatLimits
                = VRChatUtility.AvatarPerformanceStatsLevelSets[this.forQuest ? "Quest" : "PC"].Bad;

            if (statistics.PolyCount > badPerformanceStatLimits.PolyCount)
            {
                EditorGUILayout.HelpBox(string.Format(Gettext._("The number of polygons is {0}."), statistics.PolyCount)
                    + string.Format(
                        Gettext._("If a number of polygons exceeds {0}, you can not upload."),
                        badPerformanceStatLimits.PolyCount
                    ), MessageType.Error);
            }

            if (!this.forQuest)
            {
                return true;
            }

            AvatarPerformanceStatsLevel performanceStatLimits
                = VRChatUtility.AvatarPerformanceStatsLevelSets["Quest"].Medium;

            Wizard.ShowQuestLimitationsErrorMessageIfExceeds(
                current: statistics.PolyCount,
                limit: performanceStatLimits.PolyCount,
                message: Gettext._("The number of polygons is {0}.")
            );

            Wizard.ShowQuestLimitationsErrorMessageIfExceeds(
                current: statistics.SkinnedMeshCount,
                limit: performanceStatLimits.SkinnedMeshCount,
                message: Gettext._("The number of Skinned Mesh Renderer components is {0}.")
            );

            Wizard.ShowQuestLimitationsErrorMessageIfExceeds(
                current: statistics.MeshCount,
                limit: performanceStatLimits.MeshCount,
                message: Gettext._("The number of (non-Skinned) Mesh Renderer components is {0}.")
            );

            Wizard.ShowQuestLimitationsErrorMessageIfExceeds(
                current: statistics.MaterialCount,
                limit: performanceStatLimits.MaterialCount,
                message: Gettext._("The number of material slots (sub-meshes) is {0}.")
            );

            Wizard.ShowQuestLimitationsErrorMessageIfExceeds(
                current: statistics.BoneCount,
                limit: performanceStatLimits.BoneCount,
                message: Gettext._("The number of Bones is {0}.")
            );

            return true;
        }

        /// <summary>
        /// Questの制限値を超える場合にエラーメッセージを表示します。
        /// </summary>
        /// <param name="current">対象の値。</param>
        /// <param name="limit">制限値。</param>
        /// <param name="message">エラーメッセージ。</param>
        private static void ShowQuestLimitationsErrorMessageIfExceeds(int current, int limit, string message)
        {
            if (current > limit)
            {
                EditorGUILayout.HelpBox(string.Format(message, current) + string.Format(
                    Gettext._("If this value exceeds {0}, the avatar will not shown under the default user setting."),
                    limit
                ), MessageType.Error);
            }
        }

        private void OnWizardCreate()
        {
            if (string.IsNullOrEmpty(this.destinationPath))
            {
                string sourcePath = this.GetAssetsPath(vrm: this.avatar.gameObject);
                this.destinationPath = UnityPath.FromUnityPath(sourcePath).Parent
                    .Child(Path.GetFileNameWithoutExtension(sourcePath) + " (VRChat).prefab").Value;
            }
            else
            {
                UnityPath destinationFolderUnityPath = UnityPath.FromUnityPath(this.destinationPath).Parent;
                while (!destinationFolderUnityPath.IsDirectoryExists)
                {
                    destinationFolderUnityPath = destinationFolderUnityPath.Parent;
                }
                this.destinationPath = destinationFolderUnityPath.Child(Path.GetFileName(this.destinationPath)).Value;
            }

            string destinationPath = EditorUtility.SaveFilePanelInProject(
                "",
                Path.GetFileName(path: this.destinationPath),
                "prefab",
                "",
                Path.GetDirectoryName(path: this.destinationPath)
            );
            if (string.IsNullOrEmpty(destinationPath)){
                Wizard.Open(avatar: this.avatar.gameObject);
                return;
            }
            this.destinationPath = destinationPath;

            GameObject prefabInstance = Duplicator.Duplicate(
                sourceAvatar: this.avatar.gameObject,
                destinationPath: this.destinationPath,
                notCombineRendererObjectNames: this.notCombineRendererObjectNames
            );

            this.SaveSettings();

            var prefab = AssetDatabase.LoadMainAssetAtPath(this.destinationPath) as GameObject;

            foreach (VRMSpringBone springBone in this.GetSpringBonesWithComments(prefab: prefabInstance, comments: this.excludedSpringBoneComments)
                .SelectMany(springBone => springBone))
            {
                UnityEngine.Object.DestroyImmediate(springBone);
            }

            IEnumerable<Converter.Message> messages = Converter.Convert(
                prefabInstance: prefabInstance,
                clips: VRMUtility.GetAllVRMBlendShapeClips(avatar: this.avatar.gameObject),
                swayingObjectsConverterSetting: this.swayingObjects,
                takingOverSwayingParameters: this.takeOverSwayingParameters,
                swayingParametersConverter: this.swayingParametersConverter,
                enableAutoEyeMovement: this.enableEyeMovement,
                addedShouldersPositionY: this.shoulderHeights,
                fixProneAvatarPosition: this.fixProneAvatarPosition,
                moveEyeBoneToFrontForEyeMovement: this.moveEyeBoneToFrontForEyeMovement,
                forQuest: this.forQuest
            );

            if (this.postConverting != null) {
                this.postConverting(prefabInstance, prefabInstance.GetComponent<VRMMeta>());
            }

            PrefabUtility.ReplacePrefab(prefabInstance, prefab, ReplacePrefabOptions.ConnectToPrefab);

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

        /// <summary>
        /// 指定した<see cref="VRMSpringBone.m_comment" />を持つSpringBoneを返します。
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="comments"></param>
        /// <returns></returns>
        private ILookup<string, VRMSpringBone> GetSpringBonesWithComments(GameObject prefab, IEnumerable<string> comments)
        {
            return prefab.GetComponentsInChildren<VRMSpringBone>()
                .Where(bone => comments.Contains(bone.m_comment))
                .ToLookup(keySelector: bone => bone.m_comment);
        }
    }
}