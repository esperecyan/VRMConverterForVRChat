using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UniGLTF;
using VRM;
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
using VRC.Core;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.UI
{
    /// <summary>
    /// 変換ダイアログ。
    /// </summary>
    internal class Wizard : ScriptableWizard
    {
        private static readonly string EditorUserSettingsName = typeof(Wizard).Namespace;
        private static readonly string EditorUserSettingsXmlNamespace = "https://pokemori.booth.pm/items/1025226";

        /// <summary>
        /// 変換後の処理を行うコールバック関数。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="meta"></param>
        private delegate void PostConverting(GameObject avatar, VRMMeta meta);

        /// <summary>
        /// ダイアログの最小サイズ。
        /// </summary>
        private static readonly Vector2 MinSize = new Vector2(800, 350);

        /// <summary>
        /// ダイアログを開いた時点の最小の高さ。
        /// </summary>
        private static readonly int MinHeightWhenOpen = 700;

        /// <summary>
        /// リストにおける一段回分の字下げ幅。
        /// </summary>
        private static readonly int Indent = 20;

        /// <summary>
        /// 複製・変換対象のアバター。
        /// </summary>
        [SerializeField]
        private Animator avatar = default;

        /// <summary>
        /// オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。
        /// </summary>
#if VRC_SDK_VRCSDK2
        [SerializeField, Localizable]
#endif
        private bool enableEyeMovement = true;

        /// <summary> 
        /// オートアイムーブメント有効化時、目ボーンのPositionのZに加算する値。 
        /// </summary>
#if VRC_SDK_VRCSDK2
        [SerializeField, Localizable(0, 0.1f)]
#endif
        private float moveEyeBoneToFrontForEyeMovement = default;

        /// <summary>
        /// VRChat上でモデルがなで肩・いかり肩になる問題について、ボーンのPositionのYに加算する値。
        /// </summary>
        [SerializeField, Localizable(-0.1f, 0.1f)]
        private float shoulderHeights = default;

        /// <summary>
        /// VRChat上で足が沈む問題について、Hipsボーンの一つ上のオブジェクトのPositionのYに加算する値。
        /// </summary>
        [SerializeField, Localizable(-0.1f, 0.1f)]
        private float armatureHeight = default;

        /// <summary>
        /// メッシュ・サブメッシュの結合を行うなら <c>true</c>。
        /// </summary>
        [SerializeField, Localizable]
        private bool combineMeshes = true;

        /// <summary>
        /// 結合しないメッシュレンダラーのオブジェクト名。
        /// </summary>
        [SerializeField, Localizable]
        private List<string> notCombineRendererObjectNames = new List<string>();

        /// <summary>
        /// <c>false</c> の場合、シェイプキーの法線・接線を削除します。
        /// </summary>
        [SerializeField, Localizable]
        private bool useShapeKeyNormalsAndTangents = false;

        /// <summary>
        /// FINGERPOINTへ割り当てる表情を <see cref="BlendShapeClip.BlendShapeName"/> で指定。
        /// </summary>
        [SerializeField, Localizable]
        private string blendShapeForFingerpoint = "";

        [Header("For PC")]

        /// <summary>
        /// 揺れ物を変換するか否かの設定。
        /// </summary>
        [SerializeField, Localizable]
        private Converter.SwayingObjectsConverterSetting swayingObjects
            = Converter.SwayingObjectsConverterSetting.ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups;

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

        /// <summary>
        /// まばたきにAnimatorコンポーネントを利用するなら <c>true</c>。
        /// </summary>
#if VRC_SDK_VRCSDK2
        [SerializeField, Localizable]
#endif
        private bool useAnimatorForBlinks = true;

        [Header("Callback")]

        /// <summary>
        /// 各種コールバック関数のユーザー設定値。
        /// </summary>
        [SerializeField, Localizable]
        private MonoScript callbackFunctions = default;

        /// <summary>
        /// <see cref="Converter.SwayingParametersConverter"/>のユーザー設定値。
        /// </summary>
        private Converter.SwayingParametersConverter swayingParametersConverter = default;

        /// <summary>
        /// <see cref="Wizard.PostConverting"/>のユーザー設定値。
        /// </summary>
        private Wizard.PostConverting postConverting = default;

        /// <summary>
        /// 「Assets/」で始まり「.prefab」で終わる保存先のパス。
        /// </summary>
        private string destinationPath = default;

        /// <summary>
        /// 変換ダイアログを開きます。
        /// </summary>
        /// <param name="avatar"></param>
        internal static void Open(GameObject avatar)
        {
            var wizard = DisplayWizard<Wizard>(Converter.Name + " " + Converter.Version, _("Duplicate and Convert"));
            Vector2 defaultMinSize = Wizard.MinSize;
            defaultMinSize.y = Wizard.MinHeightWhenOpen;
            wizard.minSize = defaultMinSize;
            wizard.minSize = Wizard.MinSize;

            wizard.avatar = avatar.GetComponent<Animator>();

            wizard.LoadSettings();

            if (string.IsNullOrEmpty(wizard.blendShapeForFingerpoint))
            {
                var surprise = VRMUtility.GetBlendShapeClips(wizard.avatar)
                    .FirstOrDefault(clip => clip.Preset == BlendShapePreset.Unknown
                        && clip.BlendShapeName.StartsWith("Surprise", ignoreCase: true, culture: null))
                    ?.BlendShapeName;
                if (surprise != null)
                {
                    wizard.blendShapeForFingerpoint = surprise;
                }
            }

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

            var configValue = EditorUserSettings.GetConfigValue(Wizard.EditorUserSettingsName);
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
            if (settings != null)
            {
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
            var title = this.avatar.GetComponent<VRMMeta>().Meta.Title;
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
                    var value = settings.GetAttribute(info.Name);
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
            var title = this.avatar.GetComponent<VRMMeta>().Meta.Title;
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
                var fieldValue = info.GetValue(obj: this);
                var value = "";
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
                        if (string.IsNullOrEmpty(content))
                        {
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
            this.isValid = true;

            if (this.callbackFunctions)
            {
                Type callBackFunctions = this.callbackFunctions.GetClass();

                this.swayingParametersConverter = Delegate.CreateDelegate(
                    type: typeof(Converter.SwayingParametersConverter),
                    target: callBackFunctions,
                    method: "SwayingParametersConverter",
                    ignoreCase: false,
                    throwOnBindFailure: false
                ) as Converter.SwayingParametersConverter;

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
                (this.swayingParametersConverter != null ? "☑" : "☐")
                    + " public static DynamicBoneParameters SwayingParametersConverter(SpringBoneParameters, BoneInfo)",
                indentStyle
            );

            EditorGUILayout.LabelField(
                (this.postConverting != null ? "☑" : "☐")
                    + " public static void PostConverting(GameObject, VRMMeta)",
                indentStyle
            );

            if (VRChatUtility.SDKVersion == null)
            {
                EditorGUILayout.HelpBox(_("VRChat SDK2 or SDK3 has not been imported."), MessageType.Error);
                this.isValid = false;
                return true;
            }

            foreach (var type in Converter.RequiredComponents)
            {
                if (!this.avatar.GetComponent(type))
                {
                    EditorGUILayout.HelpBox(string.Format(_("Not set “{0}” component."), type), MessageType.Error);
                    this.isValid = false;
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
                    EditorGUILayout.HelpBox(string.Join(separator: "\n• ", value: new[] { _("VRMSpringBones with the below Comments do not exist.") }
                        .Concat(comments).ToArray()), MessageType.Warning);
                }
            }

            if (this.combineMeshes)
            {
                IEnumerable<string> notCombineRendererObjectNames
                    = this.notCombineRendererObjectNames.Except(new[] { "" });
                if (notCombineRendererObjectNames.Count() > 0)
                {
                    IEnumerable<string> names = notCombineRendererObjectNames.Except(
                        this.avatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                            .Concat<Component>(this.avatar.GetComponentsInChildren<MeshRenderer>())
                            .Select(renderer => renderer.name)
                    );
                    if (names.Count() > 0)
                    {
                        EditorGUILayout.HelpBox(string.Join(separator: "\n• ", value: new[] { _("Renderers on the below name GameObject do not exist.") }
                            .Concat(names).ToArray()), MessageType.Warning);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(_("If you do not “Combine Meshes”,"
                    + " and any of VRMBlendShapes references meshes other than the mesh having most shape keys"
                        + " or the mesh is not direct child of the avatar root,"
                    + " the avatar will not be converted correctly."), MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(this.blendShapeForFingerpoint)
                && !VRMUtility.GetUserDefinedBlendShapeClip(this.avatar, this.blendShapeForFingerpoint))
            {
                EditorGUILayout.HelpBox(string.Format(
                    _("There is no user-defined VRMBlensShape with the name “{0}”."),
                    this.blendShapeForFingerpoint
                ), MessageType.Warning);
            }

            var version = VRChatUtility.SDKSupportedUnityVersion;
            if (version != "" && Application.unityVersion != version)
            {
                EditorGUILayout.HelpBox(string.Format(
                    _("Unity {0} is running. If you are using a different version than {1}, VRChat SDK might not work correctly. Recommended using Unity downloaded from {2} ."),
                    Application.unityVersion,
                    version,
                    VRChatUtility.DownloadURL
                ), MessageType.Warning);
            }

            if (!this.isValid || !this.forQuest)
            {
                return true;
            }

            var currentTriangleCount = VRChatUtility.CountTriangle(this.avatar.gameObject);
            if (currentTriangleCount > VRChatUtility.Limitations.triangleCount)
            {
                EditorGUILayout.HelpBox(string.Format(
                    _("The number of polygons is {0}."),
                    currentTriangleCount
                ) + string.Format(
                    _("If this value exceeds {0}, the avatar will not shown under the default user setting."),
                    VRChatUtility.Limitations.triangleCount
                ), MessageType.Error);
            }

            return true;
        }

        private void OnWizardCreate()
        {
            if (VRChatUtility.SDKVersion == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.destinationPath))
            {
                var sourcePath = this.GetAssetsPath(vrm: this.avatar.gameObject);
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

            var destinationPath = EditorUtility.SaveFilePanelInProject(
                "",
                Path.GetFileName(path: this.destinationPath),
                "prefab",
                "",
                Path.GetDirectoryName(path: this.destinationPath)
            );
            if (string.IsNullOrEmpty(destinationPath))
            {
                Wizard.Open(avatar: this.avatar.gameObject);
                return;
            }
            this.destinationPath = destinationPath;

            // プレハブ、およびシーン上のプレハブインスタンスのBlueprint IDを取得
            var prefabBlueprintId = "";
            var blueprintIds = new Dictionary<int, string>();
            var previousPrefab = AssetDatabase.LoadMainAssetAtPath(this.destinationPath) as GameObject;
            if (previousPrefab)
            {

                var pipelineManager
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                    = previousPrefab.GetComponent<PipelineManager>();
#else
                    = (dynamic)null;
#endif
                prefabBlueprintId = pipelineManager ? pipelineManager.blueprintId : "";

                GameObject[] previousRootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                blueprintIds = previousRootGameObjects
                    .Where(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == this.destinationPath)
                    .Select(root =>
                    {
                        var manager
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                            = root.GetComponent<PipelineManager>();
#else
                            = (dynamic)null;
#endif
                        var blueprintId = manager ? manager.blueprintId : "";
                        return new
                        {
                            index = Array.IndexOf(previousRootGameObjects, root),
                            blueprintId = blueprintId != prefabBlueprintId ? blueprintId : "",
                        };
                    }).ToDictionary(
                        keySelector: indexAndBlueprintId => indexAndBlueprintId.index,
                        elementSelector: indexAndBlueprintId => (string)/* VRChat SDKがインポートされていない場合のコンパイルエラー回避 */indexAndBlueprintId.blueprintId
                    );
            }

            GameObject prefabInstance = Duplicator.Duplicate(
                sourceAvatar: this.avatar.gameObject,
                destinationPath: this.destinationPath,
                notCombineRendererObjectNames: this.notCombineRendererObjectNames,
                combineMeshesAndSubMeshes: this.combineMeshes
            );

            var messages = new List<(string, MessageType)>();

            if (this.forQuest)
            {
                messages.AddRange(VRChatUtility.CalculateQuestLimitations(prefabInstance));
            }

            this.SaveSettings();

            foreach (VRMSpringBone springBone in this.GetSpringBonesWithComments(prefab: prefabInstance, comments: this.excludedSpringBoneComments)
                .SelectMany(springBone => springBone))
            {
                UnityEngine.Object.DestroyImmediate(springBone);
            }

            var clips = VRMUtility.GetAllVRMBlendShapeClips(avatar: this.avatar.gameObject);
            messages.AddRange(Converter.Convert(
                prefabInstance: prefabInstance,
                clips: clips,
                swayingObjectsConverterSetting: this.swayingObjects,
                takingOverSwayingParameters: this.takeOverSwayingParameters,
                swayingParametersConverter: this.swayingParametersConverter,
                enableAutoEyeMovement: this.enableEyeMovement,
                addedShouldersPositionY: this.shoulderHeights,
                moveEyeBoneToFrontForEyeMovement: this.moveEyeBoneToFrontForEyeMovement,
                forQuest: this.forQuest,
                addedArmaturePositionY: this.armatureHeight,
                useAnimatorForBlinks: this.useAnimatorForBlinks,
                useShapeKeyNormalsAndTangents: this.useShapeKeyNormalsAndTangents,
                vrmBlendShapeForFINGERPOINT: !string.IsNullOrEmpty(this.blendShapeForFingerpoint)
                    ? VRMUtility.GetUserDefinedBlendShapeClip(clips, this.blendShapeForFingerpoint) as VRMBlendShapeClip
                    : null
            ));

            // 変換前のプレハブのPipeline ManagerのBlueprint IDを反映
            if (!string.IsNullOrEmpty(prefabBlueprintId))
            {
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                prefabInstance.GetComponent<PipelineManager>().blueprintId = prefabBlueprintId;
#endif
            }

            if (this.postConverting != null)
            {
                this.postConverting(prefabInstance, this.avatar.GetComponent<VRMMeta>());
            }

            PrefabUtility.ApplyPrefabInstance(prefabInstance, InteractionMode.AutomatedAction);

            // 変換前のプレハブインスタンスのPipeline ManagerのBlueprint IDを反映
            GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var (avatarIndex, blueprintId) in blueprintIds)
            {
                if (string.IsNullOrEmpty(blueprintId))
                {
                    continue;
                }
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                rootGameObjects[avatarIndex].GetComponent<PipelineManager>().blueprintId = blueprintId;
#endif
            }

            if (blueprintIds.Count > 0)
            {
                // シーンのルートに、すでに他のプレハブインスタンスが存在していれば、変換用のインスタンスは削除
                UnityEngine.Object.DestroyImmediate(prefabInstance);
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
            var path = "";

            var unityPath = UnityPath.FromAsset(asset: vrm);
            if (unityPath.IsUnderAssetsFolder)
            {
                path = unityPath.Value;
            }

            if (string.IsNullOrEmpty(path))
            {
                path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(vrm);
            }

            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/" + this.avatar.name;
            }

            return path;
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
