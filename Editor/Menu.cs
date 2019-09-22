using System.Linq;
using UnityEngine;
using UnityEditor;
using VRM;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// ウィンドウ上部のVRMメニューに追加するサブメニュー。
    /// </summary>
    public class Menu
    {
        /// <summary>
        /// 追加するメニューアイテムの、「VRM」メニュー内の位置。
        /// </summary>
        public const int Priority = 1100;

        /// <summary>
        /// 追加するメニューアイテム名。
        /// </summary>
        private const string ItemName = "VRM/Duplicate and Convert for VRChat";

        /// <summary>
        /// 選択されているアバターの変換ダイアログを開きます。
        /// </summary>
        [MenuItem(itemName: Menu.ItemName, isValidateFunction: false, priority: Menu.Priority)]
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

            var components = activeObject.GetComponentsInParent<Animator>(includeInactive: true);
            if (components.Length == 0)
            {
                return null;
            }

            return components[0].gameObject;
        }
    }
}
