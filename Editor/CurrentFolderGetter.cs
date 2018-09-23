using System.IO;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// 当エディタ拡張が保存されているフォルダのパスを取得します。
    /// </summary>
    internal class CurrentFolderGetter : ScriptableWizard
    {
        private static string currentFolderPath;

        /// <summary>
        /// 当エディタ拡張が保存されているフォルダのパスを取得します。
        /// </summary>
        /// <returns></returns>
        internal static string Get()
        {
            if (string.IsNullOrEmpty(CurrentFolderGetter.currentFolderPath)) {
                CurrentFolderGetter.currentFolderPath = Path.GetDirectoryName(
                    path: AssetDatabase.GetAssetPath(
                        assetObject: MonoScript.FromScriptableObject(scriptableObject: new CurrentFolderGetter())
                    )
                );
            }

            return CurrentFolderGetter.currentFolderPath;
        }
    }
}