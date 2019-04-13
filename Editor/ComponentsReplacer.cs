using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using VRM;
using UniGLTF;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// キャラクター情報、視点、揺れ物に関する設定。
    /// </summary>
    [InitializeOnLoad]
    public class ComponentsReplacer
    {
        /// <summary>
        /// 揺れ物を変換するか否かの設定。
        /// </summary>
        public enum SwayingObjectsConverterSetting
        {
            ConvertVrmSpringBonesOnly,
            ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups,
            RemoveSwayingObjects,
        }

        /// <summary>
        /// 揺れ物のパラメータ変換アルゴリズムの定義を行うコールバック関数。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public delegate DynamicBoneParameters SwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo);

        static ComponentsReplacer()
        {
            EnableClassDependentDependingOptionalAsset();
            FixFindDynamicBoneTypesMethodOnAvatarPerformanceClass();
        }

        /// <summary>
        /// <see cref="ComponentsReplacer.SwayingParametersConverter">の既定値。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public static DynamicBoneParameters DefaultSwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo)
        {
            return new DynamicBoneParameters()
            {
                Elasticity = springBoneParameters.StiffnessForce * 0.05f,
                Damping = springBoneParameters.DragForce * 0.6f,
                Stiffness = 0,
                Inert = 0,
            };
        }

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <returns></returns>
        internal static IEnumerable<Converter.Message> Apply(
            GameObject avatar,
            ComponentsReplacer.SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter
        )
        {
            var messages = new List<Converter.Message>();

            ConvertMeta(avatar: avatar);
            ConvertVRMFirstPerson(avatar: avatar);

            var swayingObjectsConverter = Type.GetType(typeof(ComponentsReplacer).Namespace + ".SwayingObjectsConverter, Assembly-CSharp-Editor");
            if (swayingObjectsConverter != null)
            {
                messages.AddRange(swayingObjectsConverter.InvokeMember(
                    name: "Apply",
                    invokeAttr: BindingFlags.InvokeMethod | BindingFlags.NonPublic,
                    binder: null,
                    target: null,
                    args: new object[] { avatar, swayingObjectsConverterSetting, swayingParametersConverter }
                ) as IEnumerable<Converter.Message>);
            }

            return messages;
        }

        /// <summary>
        /// Dynamic Boneアセットがインポートされていれば場合、同アセットに含まれるクラスを利用しているスクリプトファイルを有効化します。
        /// </summary>
        private static void EnableClassDependentDependingOptionalAsset()
        {
            if (Type.GetType("DynamicBone, Assembly-CSharp") != null
                && Type.GetType(typeof(ComponentsReplacer).Namespace + ".SwayingObjectsConverter, Assembly-CSharp-Editor") == null)
            {
                var path = Path.Combine(Path.Combine(Converter.RootFolderPath, "Editor"), "SwayingObjectsConverter.cs");
                AssetDatabase.MoveAsset(oldPath: path + ".bak", newPath: path);
            }
        }

        /// <summary>
        /// <see cref="AvatarPerformance"/>クラスを書き替えて、DynamicBoneに関するパフォーマンスが表示されないVRChat SDKのバグを修正します。
        /// </summary>
        /// <seealso cref="SwayingObjectsConverter.GetMessagesAboutDynamicBoneLimits"/>
        /// <remarks>
        /// 参照:
        /// SDK avatar performance reports always report dynamic bone counts as 0 | Bug Reports | VRChat
        /// <https://vrchat.canny.io/bug-reports/p/sdk-avatar-performance-reports-always-report-dynamic-bone-counts-as-0>
        /// </remarks>
        private static void FixFindDynamicBoneTypesMethodOnAvatarPerformanceClass()
        {
            string fullPath = UnityPath.FromUnityPath(VRChatUtility.AvatarPerformanceClassPath).FullPath;

            string content = File.ReadAllText(path: fullPath, encoding: Encoding.UTF8);
            if (content.Contains("DynamicBoneColliderBase"))
            {
                return;
            }

            string fixedContent = content.Replace(
                oldValue: "System.Type dyBoneColliderType = Validation.GetTypeFromName(\"DynamicBoneCollider\");",
                newValue: "System.Type dyBoneColliderType = Validation.GetTypeFromName(\"DynamicBoneColliderBase\");"
            );
            if (fixedContent == content)
            {
                return;
            }
            
            File.WriteAllText(path: fullPath, contents: fixedContent, encoding: Encoding.UTF8);
        }

        /// <summary>
        /// キャラクターに関する情報を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertMeta(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.Animations = VRChatsBugsWorkaround.DefaultAnimationSetValue;
        }

        /// <summary>
        /// <see cref="VRMFirstPerson"/>を基に<see cref="VRC_AvatarDescriptor"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertVRMFirstPerson(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            var firstPerson = avatar.GetComponent<VRMFirstPerson>();
            avatarDescriptor.ViewPosition = firstPerson.FirstPersonBone.position + firstPerson.FirstPersonOffset - avatar.transform.localPosition;
        }
    }
}