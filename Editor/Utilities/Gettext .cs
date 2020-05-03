using System;
using System.Collections.Generic;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    /// <summary>
    /// i18n。
    /// </summary>
    internal class Gettext
    {
        /// <summary>
        /// 翻訳対象文字列 (msgid) の言語。IETF言語タグの「language」サブタグ。
        /// </summary>
        private static readonly string OriginalLocale = "en";

        /// <summary>
        /// クライアントの言語の翻訳リソースが存在しないとき、どの言語に翻訳するか。IETF言語タグの「language」サブタグ。
        /// </summary>
        private static readonly string DefaultLocale = "en";

        /// <summary>
        /// クライアントの言語。<see cref="Gettext.SetLocale"/>から変更されます。
        /// </summary>
        private static string langtag = "en";

        /// <summary>
        /// クライアントの言語のlanguage部分。<see cref="Gettext.SetLocale"/>から変更されます。
        /// </summary>
        private static string language = "en";

        /// <summary>
        /// 翻訳リソース。<see cref="Gettext.SetLocalizedTexts"/>から変更されます。
        /// </summary>
        private static IDictionary<string, IDictionary<string, string>> multilingualLocalizedTexts = new Dictionary<string, IDictionary<string, string>>{ };

        /// <summary>
        /// 翻訳リソースを追加します。
        /// </summary>
        /// <param name="localizedTexts"></param>
        internal static void SetLocalizedTexts(IDictionary<string, IDictionary<string, string>> localizedTexts)
        {
            Gettext.multilingualLocalizedTexts = localizedTexts;
        }
        
        /// <summary>
        /// クライアントの言語を設定します。
        /// </summary>
        /// <param name="clientLang">IETF言語タグ (「language」と「language-REGION」にのみ対応)。</param>
        internal static void SetLocale(string clientLang)
        {
            string[] splitedClientLang = clientLang.Split(separator: '-');
            Gettext.language = splitedClientLang[0].ToLower();
            Gettext.langtag = string.Join(separator: "-", value: splitedClientLang, startIndex: 0, count: Math.Min(2, splitedClientLang.Length));
            if (Gettext.language == "ja")
            {
                // ja-JPをjaと同一視
                Gettext.langtag = Gettext.language;
            }
        }
        
        /// <summary>
        /// テキストをクライアントの言語に変換します。
        /// </summary>
        /// <param name="message">翻訳前。</param>
        /// <returns>翻訳語。</returns>
        internal static string _(string message)
        {
            if (Gettext.langtag == Gettext.OriginalLocale) {
                // クライアントの言語が翻訳元の言語なら、そのまま返す
                return message;
            }

            foreach (string langtag in new[] {
                // クライアントの言語の翻訳リソースが存在すれば、それを返す
                Gettext.langtag,
                // 地域下位タグを取り除いた言語タグの翻訳リソースが存在すれば、それを返す
                Gettext.language,
                // 既定言語の翻訳リソースが存在すれば、それを返す
                Gettext.DefaultLocale,
            }) {
                if (Gettext.multilingualLocalizedTexts.ContainsKey(key: langtag)
                    && Gettext.multilingualLocalizedTexts[Gettext.langtag].ContainsKey(key: message)
                    && Gettext.multilingualLocalizedTexts[Gettext.langtag][message] != "") {
                    return Gettext.multilingualLocalizedTexts[Gettext.langtag][message];
                }
            }

            return message;
        }
    }
}
