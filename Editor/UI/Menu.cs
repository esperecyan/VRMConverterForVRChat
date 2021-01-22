using UnityEngine;
using UnityEditor;

namespace Esperecyan.Unity.VRMConverterForVRChat.UI
{
    /// <summary>
    /// ウィンドウ上部のVRMメニューに追加するサブメニュー。
    /// </summary>
    internal class Menu
    {
        /// <summary>
        /// 追加するメニューアイテムの、「VRM」メニュー内の位置。
        /// </summary>
        private const int Priority = 1200;

        /// <summary>
        /// 追加するメニューアイテム名。
        /// </summary>
        private const string ItemName = "VRM0/Duplicate and Convert for VRChat";

        /// <summary>
        /// 追加するメニューアイテム名。
        /// </summary>
        private const string VRChatToVRMItemName = "VRM0/Export VRM file from VRChat avatar";

        /// <summary>
        /// 選択されているアバターの変換ダイアログを開きます。
        /// </summary>
        [MenuItem(Menu.ItemName, false, Menu.Priority)]
        private static void DuplicateAndConvertForVRChat()
        {
            Wizard.Open(avatar: ClosestModel());
        }

        /// <summary>
        /// 選択されている、または祖先のオブジェクトで変換可能なら<c>true</c>を返します。
        /// </summary>
        /// <returns></returns>
        [MenuItem(itemName: Menu.ItemName, isValidateFunction: true)]
        private static bool DuplicateAndConvertForVRChatIsEnable()
        {
            return ClosestModel();
        }

        /// <summary>
        /// 選択されているアバターのVRM化設定ダイアログを開きます。
        /// </summary>
        [MenuItem(Menu.VRChatToVRMItemName, isValidateFunction: false, Menu.Priority + 1)]
        private static void ExportVRM()
        {
            VRChatToVRMWizard.Open(ClosestModel());
        }

        /// <summary>
        /// 選択されている、または祖先のオブジェクトで変換可能なら<c>true</c>を返します。
        /// </summary>
        /// <returns></returns>
        [MenuItem(Menu.VRChatToVRMItemName, isValidateFunction: true)]
        private static bool ExportVRMIsEnable()
        {
            return ClosestModel();
        }

        /// <summary>
        /// 選択されている、または祖先のオブジェクトで、Animatorコンポーネントが設定されているものを取得します。
        /// </summary>
        /// <returns>見つからなかった場合は <c>null</c> を返します。</returns>
        private static GameObject ClosestModel()
        {
            var activeObject = Selection.activeObject as GameObject;
            if (!activeObject)
            {
                return null;
            }

            var components = activeObject.GetComponentsInParent<Animator>(true);
            if (components.Length == 0)
            {
                return null;
            }

            return components[0].gameObject;
        }
    }
}
