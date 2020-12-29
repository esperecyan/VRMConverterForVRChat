using System;
using UnityEngine;
using UnityEditor;
using VRM;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.UI
{
    /// <summary>
    /// 結果ダイアログ。
    /// </summary>
    internal class ErrorDialog : ScriptableWizard
    {
        private static readonly string IssuesURL = "https://github.com/esperecyan/VRMConverterForVRChat/issues";
        private static readonly string VRChatSDKVersionFilePath = "Assets/VRCSDK/version.txt";

        private Exception exception;
        private Vector2 errorMessageScrollPosition;

        /// <summary>
        /// ダイアログを開きます。
        /// </summary>
        /// <param name="messages"></param>
        internal static void Open(Exception exception)
        {
            var dialog = ScriptableWizard.DisplayWizard<ErrorDialog>(
                title: Converter.Name + " " + Converter.Version,
                createButtonName: _("Close")
            );
            dialog.exception = exception;
        }

        protected override bool DrawWizardGUI()
        {
            var errorMessage = this.exception.ToString() + "\n" + this.exception.StackTrace;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(_("Some error has occurred."));

                if (GUILayout.Button(_("Copy the content to clipboard")))
                {
                    var text = $"Unity Editor: {Application.unityVersion}\n";
                    var sdkVersion = AssetDatabase.LoadAssetAtPath<TextAsset>(ErrorDialog.VRChatSDKVersionFilePath);
                    if (sdkVersion != null)
                    {
                        text += $"VRChat SDK: {sdkVersion.text}\n";
                    }
                    text += $"{Converter.Name}: {Converter.Version}\n"
                        + $"UniVRM: {VRMVersion.VERSION}\n\n"
                        + errorMessage;
                    GUIUtility.systemCopyBuffer = text;
                }

                if (GUILayout.Button(_("Report the problem")))
                {
                    Application.OpenURL(ErrorDialog.IssuesURL);
                }
            }

            using (var scope = new EditorGUILayout.ScrollViewScope(this.errorMessageScrollPosition, GUI.skin.box))
            {
                this.errorMessageScrollPosition = scope.scrollPosition;
                GUILayout.TextArea(errorMessage);
            }

            return true;
        }
    }
}
