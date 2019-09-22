using System.Collections.Generic;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// 結果ダイアログ。
    /// </summary>
    public class ResultDialog : ScriptableWizard
    {
        private IEnumerable<Converter.Message> messages;

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        /// <param name="messages"></param>
        internal static void Open(IEnumerable<Converter.Message> messages)
        {
            var wizard = DisplayWizard<ResultDialog>(title: Converter.Name + " " + Converter.Version, createButtonName: Gettext._("OK"));
            wizard.messages = messages;
        }

        protected override bool DrawWizardGUI()
        {
            base.DrawWizardGUI();
            EditorGUILayout.LabelField(label: Gettext._("Converting is completed."));
            foreach (var message in this.messages) {
                EditorGUILayout.HelpBox(message: message.message, type: message.type);
            }
            return true;
        }

        private void OnWizardCreate()
        {
        }
    }
}
