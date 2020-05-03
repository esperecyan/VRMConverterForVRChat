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
        private IEnumerable<Converter.Message> messages;

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        /// <param name="messages"></param>
        internal static void Open(IEnumerable<Converter.Message> messages)
        {
            var wizard = DisplayWizard<ResultDialog>(Converter.Name + " " + Converter.Version, _("OK"));
            wizard.messages = messages;
        }

        protected override bool DrawWizardGUI()
        {
            base.DrawWizardGUI();
            EditorGUILayout.LabelField(_("Converting is completed."));
            foreach (var message in this.messages) {
                EditorGUILayout.HelpBox(message.message, message.type);
            }
            return true;
        }

        private void OnWizardCreate()
        {
        }
    }
}
