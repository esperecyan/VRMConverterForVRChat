using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// L10N。
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizableAttribute))]
    internal class Locales : PropertyDrawer
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Gettext.SetLocalizedTexts(localizedTexts: new Dictionary<string, IDictionary<string, string>> {
                { "ja", new Dictionary<string, string> {
                    { "Enable Eye Movement", "視線追従を有効化" },
                    { "Move Eye Bone To Front For Eye Movement", "目ボーンを手前に移動(視線用)" },
                    { "Swaying Objects", "揺れ物" },
                    { "Take Over Swaying Parameters", "揺れパラメータ引き継ぎ" },
                    { "Shoulder Heights", "肩の高さ" },
                    { "Armature Height", "Armatureの高さ" },
                    { "Fix Prone Avatar Position", "伏せアバターの位置ズレ補正" },
                    { "Use Animator For Blinks", "まばたきにAnimatorを使用" },
                    { "Callback Functions", "コールバック関数" },
                    { "Not set “{0}” component.", "{0}コンポーネントが設定されていません。" },
                    { "VRMSpringBones with the below Comments do not exist.", "以下のCommentを持つVRMSpringBoneは存在しません。" },
                    { "Renderers on the below name GameObject do not exist.", "レンダラーが設定されたGameObjectのうち、以下の名前を持つものは存在しません。" },
                    { "Unity {0} is running. If you are using a different version than {1}, VRChat SDK might not work correctly. Recommended using Unity downloaded from {2} .",
                        "Unity {0} が起動しています。{1} 以外のバージョンでは、VRChat SDK が正常に動作しない可能性があります。{2} からダウンロードした Unity の利用を推奨します。" },
                    { "The number of polygons is {0}.", "ポリゴン数が{0}です。"},
                    { "The number of Skinned Mesh Renderer components is {0}.", "Skinned Mesh Rendererコンポーネントの数 (ウェイトが設定されているメッシュ数) が{0}です。"},
                    { "The number of (non-Skinned) Mesh Renderer is {0}.", "Mesh Rendererコンポーネントの数 (ウェイトが設定されていないメッシュ数) が{0}です。"},
                    { "The number of material slots (sub-meshes) is {0}.", "マテリアルスロット数 (サブメッシュ数) が{0}です。"},
                    { "The number of Bones is {0}.", "ボーン数が{0}です。"},
                    { "If this value exceeds {0}, the avatar will not shown under the default user setting.",
                        "この値が{0}を超えていると、デフォルトのユーザー設定ではアバターが表示されません。" },
                    { "The “Dynamic Bone Simulated Bone Count” is {0}.",
                        "「Dynamic Bone Simulated Bone Count」(Dynamic Boneで揺れるボーンの数) が{0}です。" },
                    { "The “Dynamic Bone Collision Check Count” is {0}.",
                        "「Dynamic Bone Collision Check Count」(Dynamic Boneで揺れるボーンの数×Dynamic Bone Colliderの数) が{0}です。" },
                    { "If this value exceeds {0}, the default user setting disable all Dynamic Bones.",
                        "この値が{0}を超えていると、デフォルトのユーザー設定ではすべてのDynamic Boneが無効化されます。" },
                    { "Duplicate and Convert", "複製して変換" },
                    { "The shoulders is in {0} Unit. You can not upload, if the shoulders is not in over than {1} Unit.",
                        "肩が {0} Unit の位置にあります。{1} Unit 以上でなければアップロードできません。" },
                    { "The avatar is scaled to {0} times to be settled in uploadable shoulders height {1} Unit.",
                        "アバターを{0}倍に拡大し、アップロード可能な肩の高さ {1} Unit を超えるようにしました。" },
                    { "Converting these materials (for VRChat Render Queue bug) was failed.",
                        "以下のマテリアルの変換 (VRChatのRender Queueバグ対策) が失敗しました。" },
                    { "Converting is completed.", "変換が完了しました。" },
                    { "OK", "OK" },
                }}
            });
            
            Gettext.SetLocale(clientLang: Locales.ConvertToLangtagFromSystemLanguage(systemLanguage: Application.systemLanguage));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label.text = Gettext._(label.text);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    var attribute = (LocalizableAttribute)this.attribute;
                    EditorGUI.Slider(position, property, attribute.min, attribute.max, label);
                    break;
                default:
                    EditorGUI.PropertyField(position, property, label);
                    break;
            }
        }

        /// <summary>
        /// <see cref="SystemLanguage"/>に対応するIETF言語タグを返します。
        /// </summary>
        /// <param name="systemLanguage"></param>
        /// <returns><see cref="SystemLanguage.Unknown"/>の場合は「und」、未知の<see cref="SystemLanguage"/>の場合は空文字列を返します。</returns>
        private static string ConvertToLangtagFromSystemLanguage(SystemLanguage systemLanguage)
        {
            switch (systemLanguage) {
                case SystemLanguage.Afrikaans:
                    return "af";
                case SystemLanguage.Arabic:
                    return "ar";
                case SystemLanguage.Basque:
                    return "eu";
                case SystemLanguage.Belarusian:
                    return "be";
                case SystemLanguage.Bulgarian:
                    return "bg";
                case SystemLanguage.Catalan:
                    return "ca";
                case SystemLanguage.Chinese:
                    return "zh";
                case SystemLanguage.Czech:
                    return "cs";
                case SystemLanguage.Danish:
                    return "da";
                case SystemLanguage.Dutch:
                    return "nl";
                case SystemLanguage.English:
                    return "en";
                case SystemLanguage.Estonian:
                    return "et";
                case SystemLanguage.Faroese:
                    return "fo";
                case SystemLanguage.Finnish:
                    return "fi";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.Greek:
                    return "el";
                case SystemLanguage.Hebrew:
                    return "he";
                case SystemLanguage.Hungarian:
                    return "hu";
                case SystemLanguage.Icelandic:
                    return "is";
                case SystemLanguage.Indonesian:
                    return "in";
                case SystemLanguage.Italian:
                    return "it";
                case SystemLanguage.Japanese:
                    return "ja";
                case SystemLanguage.Korean:
                    return "ko";
                case SystemLanguage.Latvian:
                    return "lv";
                case SystemLanguage.Lithuanian:
                    return "lt";
                case SystemLanguage.Norwegian:
                    return "no";
                case SystemLanguage.Polish:
                    return "pl";
                case SystemLanguage.Portuguese:
                    return "pt";
                case SystemLanguage.Romanian:
                    return "ro";
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.SerboCroatian:
                    return "sh";
                case SystemLanguage.Slovak:
                    return "sk";
                case SystemLanguage.Slovenian:
                    return "sl";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.Swedish:
                    return "sv";
                case SystemLanguage.Thai:
                    return "th";
                case SystemLanguage.Turkish:
                    return "tr";
                case SystemLanguage.Ukrainian:
                    return "uk";
                case SystemLanguage.Vietnamese:
                    return "vi";
                case SystemLanguage.ChineseSimplified:
                    return "zh-Hans";
                case SystemLanguage.ChineseTraditional:
                    return "zh-Hant";
                case SystemLanguage.Unknown:
                    return "und";
            }

            return "";
        }
    }
}
