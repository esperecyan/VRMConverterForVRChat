using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniHumanoid;
using UniGLTF;
#if VRC_SDK_VRCSDK3
using VRC.SDKBase;
#endif
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using static Esperecyan.Unity.VRMConverterForVRChat.Utilities.Gettext;

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// VRChatの不具合などに対処します。
    /// </summary>
    internal class VRChatsBugsWorkaround
    {
        /// <summary>
        /// VRChat上でなで肩・いかり肩になる問題を解消するために変更する必要があるボーン。
        /// </summary>
        /// 参照:
        /// VRoid studioで作ったモデルをVRChatにアップロードする際の注意点 — yupaがエンジニアになるまでを記録するブログ
        /// <https://yu8as.hatenablog.com/entry/2018/08/25/004856>
        /// 猫田あゆむ🐈VTuber｜仮想秘密結社「ネコミミナティ」さんのツイート: “何度もすみません。FBXのRigからBone座標を設定する場合は、ShoulderのY座標をチョイあげ（0.12...くらい）、Upper ArmのY座標を0にするといい感じになるそうです。もしかしたらコレVRoidのモデル特有の話かもしれないのですが・・・。… https://t.co/d7Jw7qoXBX”
        /// <https://twitter.com/virtual_ayumu/status/1051146511197790208>
        internal static readonly IEnumerable<HumanBodyBones> RequiredModifiedBonesForVRChat = new[]{
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm
        };

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="keepingUpperChest"></param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="addedArmaturePositionY"></param>
        /// <returns>変換中に発生したメッセージ。</returns>
        internal static IEnumerable<(string, MessageType)> Apply(
            GameObject avatar,
            bool keepingUpperChest,
            float addedShouldersPositionY,
            float addedArmaturePositionY
        )
        {
            var messages = new List<(string, MessageType)>();

            VRChatsBugsWorkaround.EnableAnimationOvrride(avatar: avatar);
            if (!keepingUpperChest)
            {
                VRChatsBugsWorkaround.RemoveUpperChest(avatar);
            }
            VRChatsBugsWorkaround.AddShouldersPositionYAndEyesPositionZ(
                avatar: avatar,
                addedValueToArmature: addedArmaturePositionY,
                addedValueToShoulders: addedShouldersPositionY
            );
            messages.AddRange(VRChatsBugsWorkaround.EnableTextureMipmapStreaming(avatar: avatar));

            return messages;
        }

        /// <summary>
        /// 指のボーンを補完し、アニメーションオーバーライドが機能するようにします。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// 車軸制作所🌀mAtEyYEyLYE ouwua raudl/.さんのツイート: “Humanoidにしてるのになんで手の表情アニメーションオーバーライド動かないだーってなってたけど解決 ちゃんと指のボーンもHumanoidに対応づけないとダメなのね”
        /// <https://twitter.com/shajiku_works/status/977811702921150464>
        /// </remarks>
        /// <param name="avatar"></param>
        private static void EnableAnimationOvrride(GameObject avatar)
        {
            AvatarDescription avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            IEnumerable<HumanBodyBones> existedHumanBodyBones = avatarDescription.human.Select(boneLimit => boneLimit.humanBone);

            IEnumerable<BoneLimit> addedBoneLimits = VRChatUtility.RequiredHumanBodyBonesForAnimationOverride.Select(parentAndChildren =>
            {
                Transform parent = avatar.GetComponent<Animator>().GetBoneTransform(parentAndChildren.Key);
                return parentAndChildren.Value.Except(existedHumanBodyBones).Select(child =>
                {
                    Transform dummyBone = new GameObject("vrc." + child).transform;
                    dummyBone.parent = parent;
                    parent = dummyBone;
                    return new BoneLimit() { humanBone = child, boneName = dummyBone.name };
                });
            }).ToList().SelectMany(boneLimit => boneLimit);

            if (addedBoneLimits.Count() == 0)
            {
                return;
            }

            avatarDescription.human = avatarDescription.human.Concat(addedBoneLimits).ToArray();
            ApplyAvatarDescription(avatar: avatar);
        }

        /// <summary>
        /// <see cref="Avatar"/>を作成して保存し、アバターに設定します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="humanDescriptionModifier"><see cref="AvatarDescription.ToHumanDescription"/>によって生成された<see cref="HumanDescription"/>を変更するコールバック関数。
        ///     再度メソッドを呼び出すと変更は失われます。</param>
        private static void ApplyAvatarDescription(
            GameObject avatar,
            Action<HumanDescription> humanDescriptionModifier = null
        )
        {
            var humanoidDescription = avatar.GetComponent<VRMHumanoidDescription>();
            AvatarDescription avatarDescription = humanoidDescription.Description;
            var humanDescription = avatarDescription.ToHumanDescription(avatar.transform);
            if (humanDescriptionModifier != null)
            {
                humanDescriptionModifier(humanDescription);
            }
            Avatar humanoidRig = AvatarBuilder.BuildHumanAvatar(avatar, humanDescription);
            humanoidRig.name = humanoidDescription.Avatar.name;
            EditorUtility.CopySerialized(humanoidRig, humanoidDescription.Avatar);
            PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);
            EditorUtility.SetDirty(humanoidDescription.Avatar);
        }

        /// <summary> 
        /// <see cref="HumanBodyBones.UpperChest"/>が存在する場合、それを<see cref="HumanBodyBones.Chest"/>とし、元の<see cref="HumanBodyBones.Chest"/>の関連付けを外します。 
        /// </summary>
        /// <param name="avatar"></param> 
        private static void RemoveUpperChest(GameObject avatar)
        {
            var avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            var boneLimits = avatarDescription.human.ToList();
            var upperChest = boneLimits.FirstOrDefault(boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest);
            if (string.IsNullOrEmpty(upperChest.boneName))
            {
                return;
            }

            boneLimits.Remove(boneLimits.First(boneLimit => boneLimit.humanBone == HumanBodyBones.Chest));

            upperChest.humanBone = HumanBodyBones.Chest;
            boneLimits[boneLimits.FindIndex(boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest)] = upperChest;

            avatarDescription.human = boneLimits.ToArray();
            VRChatsBugsWorkaround.ApplyAvatarDescription(avatar);
        }

        /// <summary>
        /// VRChat上で発生するの以下の問題に対処するため、ボーンのPositionを変更します。
        /// • 足が沈む
        /// • なで肩・いかり肩になる
        /// • オートアイムーブメント有効化に伴うウェイト塗り直しで黒目が白目に沈む
        /// </summary>
        /// <remarks>
        /// 参照:
        /// WiLさんのツイート: “#VRChat blender無しでアバターを浮かせる(靴が埋まらないようにする)方法 1. fbxファイル(prefabではない)→rig→configureを選択 2. rig設定内HierarchyのArmature→Transformで高さ(y position)を浮かせたい値だけ増やす→Done 3. Avatar DescriptorのView Positionを浮かせたい値と同じだけ増やす… https://t.co/fdMtnuQqy1”
        /// <https://twitter.com/WiL_VRC/status/1147723536716296192>
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="addedValueToArmature"></param>
        /// <param name="addedValueToShoulders"></param>
        private static void AddShouldersPositionYAndEyesPositionZ(
            GameObject avatar,
            float addedValueToArmature,
            float addedValueToShoulders
        )
        {
            if (addedValueToArmature == 0.0f && addedValueToShoulders == 0.0f)
            {
                return;
            }

            ApplyAvatarDescription(avatar: avatar, humanDescriptionModifier: humanDescription =>
            {
                var humanBones = humanDescription.human.ToList();
                var skeltonBones = humanDescription.skeleton.ToList();
                if (addedValueToArmature != 0.0f)
                {
                    var addedPosition = new Vector3(0, addedValueToArmature, 0);

                    var armatureName
                        = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).parent.name;
                    humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == armatureName)].position
                        += addedPosition;

#if VRC_SDK_VRCSDK3
                    avatar.GetComponent<VRC_AvatarDescriptor>().ViewPosition += addedPosition;
#endif
                }
                if (addedValueToShoulders != 0.0f)
                {
                    foreach (HumanBodyBones bone in VRChatsBugsWorkaround.RequiredModifiedBonesForVRChat)
                    {
                        var humanName = bone.ToString();
                        var name = humanBones.Find(match: humanBone => humanBone.humanName == humanName).boneName;
                        humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == name)].position
                            += new Vector3(0, addedValueToShoulders, 0);
                    }
                }
            });
        }

        /// <summary>
        /// テクスチャのMipmap Streamingが無効だとアップロードできないため、有効化します。
        /// </summary>
        /// <param name="avatar"></param>
        private static IEnumerable<(string, MessageType)> EnableTextureMipmapStreaming(GameObject avatar)
        {
            var messages = new List<(string, MessageType)>();

            var paths = new List<string>();
            foreach (Texture texture
                in EditorUtility.CollectDependencies(new[] { avatar }).Where(obj => obj is Texture))
            {
                var path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer || !importer.mipmapEnabled || importer.streamingMipmaps)
                {
                    continue;
                }

                importer.streamingMipmaps = true;
                EditorUtility.SetDirty(importer);
                paths.Add(path);
            }

            if (paths.Count == 0)
            {
                return messages;
            }

            AssetDatabase.ForceReserializeAssets(paths);

            messages.Add((string.Join(
                separator: "\n• ",
                value: new[] { _("“Texture Mipmap Streaming” was enabled on these each textures.") }
                    .Concat(paths).ToArray()
            ), MessageType.Warning));

            return messages;
        }
    }
}
