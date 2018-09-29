using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniHumanoid;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRChatの不具合などに対処します。
    /// </summary>
    public class VRChatsBugsWorkaround
    {
        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="assetsPath"></param>
        internal static void Apply(GameObject avatar, string assetsPath)
        {
            VRChatsBugsWorkaround.AdjustHumanDescription(avatar: avatar, assetsPath: assetsPath);
        }

        /// <summary>
        /// <see cref="HumanBodyBones.UpperChest"/>が存在する場合、それを<see cref="HumanBodyBones.Chest"/>とし、元の<see cref="HumanBodyBones.Chest"/>の関連付けは外すようにした。
        /// </summary>
        /// <seealso cref="VRC_SdkControlPanel.AnalyzeIK"/>
        /// <param name="avatar"></param>
        /// <param name="assetsPath"></param>
        private static void AdjustHumanDescription(GameObject avatar, string assetsPath)
        {
            var humanoidDescription = avatar.GetComponent<VRMHumanoidDescription>();
            bool isCreated;
            AvatarDescription avatarDescription = humanoidDescription.GetDescription(isCreated: out isCreated);

            List<BoneLimit> boneLimits = avatarDescription.human.ToList();
            var upperChest = boneLimits.FirstOrDefault(predicate: boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest);
            if (string.IsNullOrEmpty(upperChest.boneName)) {
                return;
            }

            avatarDescription = VRChatsBugsWorkaround.DuplicateObject(avatar: avatar, assetsPath: assetsPath, obj: avatarDescription) as AvatarDescription;

            boneLimits.Remove(boneLimits.First(predicate: boneLimit => boneLimit.humanBone == HumanBodyBones.Chest));

            upperChest.humanBone = HumanBodyBones.Chest;
            boneLimits[boneLimits.FindIndex(boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest)] = upperChest;

            avatarDescription.human = boneLimits.ToArray();

            Avatar humanoidRig = AvatarBuilder.BuildHumanAvatar(
                go: avatar,
                humanDescription: avatarDescription.ToHumanDescription(root: avatar.transform)
            );
            AssetDatabase.CreateAsset(
                asset: humanoidRig,
                path: Path.Combine(Converter.GetAnimationsFolderPath(avatar: avatar, assetsPath: assetsPath), humanoidDescription.Avatar.name + ".asset")
            );
            avatar.GetComponent<Animator>().avatar = humanoidRig;
            EditorUtility.SetDirty(target: humanoidRig);
        }

        /// <summary>
        /// 変換前のアバターに関連するオブジェクトを複製して返します。
        /// </summary>
        /// <param name="avatar">複製したアバター。</param>
        /// <param name="assetsPath"></param>
        /// <returns>すでに複製されていた場合、そのまま返します。</returns>
        private static UnityEngine.Object DuplicateObject(GameObject avatar, string assetsPath, UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(assetObject: obj);
            var newPath = Path.Combine(Converter.GetAnimationsFolderPath(avatar: avatar, assetsPath: assetsPath), obj.name + ".asset");
            if (path != newPath)
            {
                obj = GameObject.Instantiate(original: obj);
                AssetDatabase.CreateAsset(asset: obj, path: newPath);
            }
            EditorUtility.SetDirty(target: obj);
            return obj;
        }
    }
}
