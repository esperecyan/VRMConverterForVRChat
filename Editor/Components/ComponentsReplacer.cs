using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniGLTF;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using Esperecyan.UniVRMExtensions.SwayingObjects;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// キャラクター情報、視点、揺れ物に関する設定。
    /// </summary>
    internal class ComponentsReplacer
    {
        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="swayingParametersConverter"></param>
        /// <returns></returns>
        internal static IEnumerable<(string, MessageType)> Apply(
            GameObject avatar,
            Converter.SwayingObjectsConverterSetting swayingObjectsConverterSetting,
            VRMSpringBonesToVRCPhysBonesConverter.ParametersConverter swayingParametersConverter,
            bool forQuest
        )
        {
            var messages = new List<(string, MessageType)>();

            ConvertVRMFirstPerson(avatar: avatar);

            if (swayingObjectsConverterSetting == Converter.SwayingObjectsConverterSetting.RemoveSwayingObjects)
            {
                return messages;
            }

            var ignoreColliders
                = swayingObjectsConverterSetting == Converter.SwayingObjectsConverterSetting.ConvertVrmSpringBonesOnly;
            if (!ignoreColliders)
            {
                ComponentsReplacer.RemoveUnusedColliderGroups(avatar);
            }

            var animator = avatar.GetComponent<Animator>();
            VRMSpringBonesToVRCPhysBonesConverter.Convert(
                source: animator,
                destination: animator,
                ignoreColliders: ignoreColliders,
                parametersConverter: swayingParametersConverter
            );

            return messages;
        }

        /// <summary>
        /// <see cref="VRMFirstPerson"/>を基に<see cref="VRC_AvatarDescriptor"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertVRMFirstPerson(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
            var firstPerson = avatar.GetComponent<VRMFirstPerson>();
            avatarDescriptor.ViewPosition = firstPerson.FirstPersonBone.position + firstPerson.FirstPersonOffset - avatar.transform.localPosition;
        }

        /// <summary>
        /// <see cref="VRMSpringBone.ColliderGroups"/>から参照されていない<see cref="VRMSpringBoneColliderGroup"/>を削除します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void RemoveUnusedColliderGroups(GameObject avatar)
        {
            IEnumerable<GameObject> objectsHavingUsedColliderGroup = avatar.GetComponentsInChildren<VRMSpringBone>()
                .SelectMany(springBone => springBone.ColliderGroups)
                .Select(colliderGroup => colliderGroup.gameObject)
                .ToArray();

            foreach (var colliderGroup in avatar.GetComponentsInChildren<VRMSpringBoneColliderGroup>())
            {
                if (!objectsHavingUsedColliderGroup.Contains(colliderGroup.gameObject))
                {
                    UnityEngine.Object.DestroyImmediate(colliderGroup);
                }
            }
        }
    }
}
