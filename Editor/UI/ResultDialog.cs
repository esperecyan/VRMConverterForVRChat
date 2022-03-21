using System.Collections.Generic;
using UnityEditor;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.UI
{
    /// <summary>
    /// 結果ダイアログ。
    /// </summary>
    internal class ResultDialog : ScriptableWizard
    {
        private IEnumerable<(string message, MessageType type)> messages;

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        /// <param name="version">当エディタ拡張のバージョン。</param>
        /// <param name="messages"></param>
        internal static void Open(string version, IEnumerable<(string, MessageType)> messages)
        {
            var wizard = DisplayWizard<ResultDialog>(Converter.Name + " " + version, _("OK"));
            wizard.messages = messages;
        }

        protected override bool DrawWizardGUI()
        {
            base.DrawWizardGUI();
            EditorGUILayout.LabelField(_("Converting is completed."));
            foreach (var (message, type) in this.messages)
            {
                EditorGUILayout.HelpBox(message, type);
            }
            return true;
        }

        private void OnWizardCreate()
        {
        }
    }
}
