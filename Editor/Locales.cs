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
                    { "Fix Vroid Sloping Shoulders", "VRoidのなで肩解消" },
                    { "Use Old Mtoon", "MToon 1.7を使用" },
                    { "Callback Functions", "コールバック関数" },
                    { "Not set “{0}” component.", "{0}コンポーネントが設定されていません。" },
                    { "Unity {0} is running. If you are using a different version than {1}, VRChat SDK might not work correctly. Recommended using Unity downloaded from {2} .",
                        "Unity {0} が起動しています。{1} 以外のバージョンでは、VRChat SDK が正常に動作しない可能性があります。{2} からダウンロードした Unity の利用を推奨します。" },
                    { "The number of polygons is {0}. If a number of polygons exceeds {1}, you can not upload.",
                        "ポリゴン数が{0}です。ポリゴン数が{1}を超える場合、アップロードできません。" },
                    { "Duplicate and Convert", "複製して変換" },
                    { "The shoulders is in {0} Unit. You can not upload, if the shoulders is not in over than {1} Unit.",
                        "肩が {0} Unit の位置にあります。{1} Unit 以上でなければアップロードできません。" },
                    { "The avatar is scaled to {0} times to be settled in uploadable shoulders height {1} Unit.",
                        "アバターを{0}倍に拡大し、アップロード可能な肩の高さ {1} Unit を超えるようにしました。" },
                    { "Converting is completed.", "変換が完了しました。" },
                    { "OK", "OK" },
                }}
            });
            
            Gettext.SetLocale(clientLang: Locales.ConvertToLangtagFromSystemLanguage(systemLanguage: Application.systemLanguage));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label.text = Gettext._(label.text);
            EditorGUI.PropertyField(position, property, label);
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
