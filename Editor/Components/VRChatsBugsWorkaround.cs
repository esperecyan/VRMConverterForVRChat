using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRM;
using UniHumanoid;
using UniGLTF;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
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
#if VRC_SDK_VRCSDK2
        /// <summary>
        /// 正常に動作する<see cref="VRC_AvatarDescriptor.Animations"/>の値。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// キノスラさんのツイート: “・男の子でもVRC_Avatar Descriptorの設定はFemaleにしておいた方が良さげ。Maleだと脚の開き方とかジャンプポーズに違和感が。 ・DynamicBoneの動きがUnity上で揺らした時とはだいぶ違う。”
        /// <https://twitter.com/cinosura_/status/1063106430947930112>
        /// </remarks>
        internal static readonly VRC_AvatarDescriptor.AnimationSet DefaultAnimationSetValue
            = VRC_AvatarDescriptor.AnimationSet.Female;
#endif

        /// <summary>
        /// 【SDK2】オートアイムーブメントにおける目のボーンの回転角度の最大値。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// Eye trackingの実装【VRChat技術情報】 — VRChatパブリックログ
        /// <https://jellyfish-qrage.hatenablog.com/entry/2018/07/25/034610>
        /// </remarks>
        internal static readonly int MaxAutoEyeMovementDegree = 30;

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
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="addedArmaturePositionY"></param>
        /// <param name="moveEyeBoneToFrontForEyeMovement"></param>
        /// <param name="forQuest"></param>
        /// <returns>変換中に発生したメッセージ。</returns>
        internal static IEnumerable<(string, MessageType)> Apply(
            GameObject avatar,
            bool enableAutoEyeMovement,
            float addedShouldersPositionY,
            float addedArmaturePositionY,
            float moveEyeBoneToFrontForEyeMovement,
            bool forQuest
        )
        {
            var messages = new List<(string, MessageType)>();

            VRChatsBugsWorkaround.EnableAnimationOvrride(avatar: avatar);
            if (VRChatUtility.SDKVersion == 2)
            {
                if (enableAutoEyeMovement)
                {
                    if (!VRChatsBugsWorkaround.ApplyAutoEyeMovementDegreeMapping(avatar: avatar))
                    {
                        moveEyeBoneToFrontForEyeMovement = 0.0f;
                    }
                }
                else
                {
                    VRChatsBugsWorkaround.DisableAutoEyeMovement(avatar: avatar);
                    moveEyeBoneToFrontForEyeMovement = 0.0f;
                }
            }
            else
            {
                moveEyeBoneToFrontForEyeMovement = 0.0f;
            }
            VRChatsBugsWorkaround.AddShouldersPositionYAndEyesPositionZ(
                avatar: avatar,
                addedValueToArmature: addedArmaturePositionY,
                addedValueToShoulders: addedShouldersPositionY,
                addedValueToEyes: moveEyeBoneToFrontForEyeMovement
            );
            if (VRChatUtility.SDKVersion == 2)
            {
                if (enableAutoEyeMovement || forQuest)
                {
                    // VRChatsBugsWorkaround.AddShouldersPositionYAndEyesPositionZ() より後に実行しないと
                    // 同メソッド内部で使用しているUniVRMが、同名ボーンのエラーを出す場合がある
                    VRChatsBugsWorkaround.EnableAutoEyeMovement(avatar: avatar);
                }
            }
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
        /// 【SDK2】オートアイムーブメントが有効化される条件を揃えます。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// 100の人さんのツイート: “Body当たりでした！　オートアイムーブメントの条件解明！ • ルート直下に、BlendShapeが4つ以上設定された「Body」という名前のオブジェクトが存在する • ルート直下に Armature/Hips/Spine/Chest/Neck/Head/RightEyeとLeftEye 　※すべて空のオブジェクトで良い 　※目のボーンの名称は何でも良い… https://t.co/dLnHl7QjJk”
        /// <https://twitter.com/esperecyan/status/1045713562348347392>
        /// </remarks>
        /// <param name="avatar"></param>
        private static void EnableAutoEyeMovement(GameObject avatar)
        {
            // ダミーの階層構造の作成
            foreach (var path in VRChatUtility.RequiredPathForAutoEyeMovement.Concat(new string[] { VRChatUtility.AutoBlinkMeshPath }))
            {
                var current = avatar.transform;
                foreach (var name in path.Split(separator: '/'))
                {
                    Transform child = current.Find(name);
                    if (!child)
                    {
                        child = new GameObject(name).transform;
                        child.parent = current;
                    }
                    current = child;
                }
            }

            // ダミーのまばたき用ブレンドシェイプの作成
            Mesh mesh = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetSharedMesh();
            if (mesh.blendShapeCount >= BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Count())
            {
                return;
            }

            foreach (var name in BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Skip(count: mesh.blendShapeCount))
            {
                BlendShapeReplacer.AddDummyShapeKey(mesh: mesh, name: name);
            }

            EditorUtility.SetDirty(mesh);
        }

        /// <summary>
        /// 【SDK2】オートアイムーブメントが有効化される条件が揃っていれば、目ボーンの関連付けを外します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void DisableAutoEyeMovement(GameObject avatar)
        {
            if (!VRChatUtility.IsEnabledAutoEyeMovementInSDK2(avatar))
            {
                return;
            }

            var eyeBones = new[] { HumanBodyBones.RightEye, HumanBodyBones.LeftEye }
                .Select(id => avatar.GetComponent<Animator>().GetBoneTransform(id))
                .Where(bone => bone);
            if (eyeBones.Count() == 0)
            {
                return;
            }

            AvatarDescription avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            var boneLimits = avatarDescription.human.ToList();
            foreach (Transform bone in eyeBones)
            {
                var index = boneLimits.FindIndex(match: limit => limit.boneName == bone.name);
                bone.name = bone.name.ToLower();
                BoneLimit boneLimit = boneLimits[index];
                boneLimit.boneName = bone.name;
                boneLimits[index] = boneLimit;
            }

            avatarDescription.human = boneLimits.ToArray();
            ApplyAvatarDescription(avatar: avatar);
        }

        /// <summary>
        /// 【SDK2】オートアイムーブメントの目ボーンの角度を、<see cref="VRMLookAtBoneApplyer"/>で指定された角度のうち最小値になるようにウェイトペイントを行います。
        /// </summary>
        /// <param name="avatar"></param>
        /// <remarks>
        /// 参照:
        /// Eye trackingの実装【VRChat技術情報】 — VRChatパブリックログ
        /// <https://jellyfish-qrage.hatenablog.com/entry/2018/07/25/034610>
        /// 海行プログラムさんのツイート: “自前でスキンメッシュをどうこうするにあたって役に立ったUnityマニュアルのコード。bindposeってのを各ボーンに設定しないといけないんだけど、ボーンのtransform.worldToLocalMatrixを入れればＯＫ　　https://t.co/I2qKb6uQ8a”
        /// <https://twitter.com/kaigyoPG/status/807648864081616896>
        /// </remarks>
        /// <returns>塗り直しを行った場合は <c>true</c> を返します。</returns>
        private static bool ApplyAutoEyeMovementDegreeMapping(GameObject avatar)
        {
            var lookAtBoneApplyer = avatar.GetComponent<VRMLookAtBoneApplyer>();
            if (!lookAtBoneApplyer)
            {
                return false;
            }

            var animator = avatar.GetComponent<Animator>();
            Transform[] eyes = new[] { HumanBodyBones.RightEye, HumanBodyBones.LeftEye }
                .Select(id => animator.GetBoneTransform(id))
                .Where(transform => transform)
                .ToArray();
            if (eyes.Length == 0)
            {
                return false;
            }

            var renderer = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetComponent<SkinnedMeshRenderer>();

            Transform[] bones = renderer.bones;
            ILookup<Transform, int> boneIndicesAndBones = bones.Select((bone, index) => new { bone, index })
                .ToLookup(
                    keySelector: boneAndIndex => boneAndIndex.bone,
                    elementSelector: boneAndIndex => boneAndIndex.index
                );
            IEnumerable<int> eyeBoneIndexes = eyes.SelectMany(eye => eye.GetComponentsInChildren<Transform>())
                .SelectMany(eyeBone => boneIndicesAndBones[eyeBone]).Where(index => index >= 0);
            if (eyeBoneIndexes.Count() == 0)
            {
                return false;
            }

            Mesh mesh = renderer.sharedMesh;
            EditorUtility.SetDirty(mesh);

            var minDegree = new[] { lookAtBoneApplyer.HorizontalOuter, lookAtBoneApplyer.HorizontalInner, lookAtBoneApplyer.VerticalDown, lookAtBoneApplyer.VerticalUp }
                .Select(mapper => mapper.CurveYRangeDegree)
                .Min();
            var eyeBoneWeight = minDegree / VRChatsBugsWorkaround.MaxAutoEyeMovementDegree;
            var headBoneWeight = 1 - eyeBoneWeight;

            Transform headBone = avatar.GetComponent<VRMFirstPerson>().FirstPersonBone;
            var headBoneIndicesAndBindposes = boneIndicesAndBones[headBone]
                .Select(index => new { index, bindpose = mesh.bindposes[index] }).ToList();

            mesh.boneWeights = mesh.boneWeights.Select(boneWeight =>
            {
                IEnumerable<float> weights = new[] { boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3 }.Where(weight => weight > 0);
                IEnumerable<int> boneIndexes = new[] { boneWeight.boneIndex0, boneWeight.boneIndex1, boneWeight.boneIndex2, boneWeight.boneIndex3 }.Take(weights.Count());
                if (eyeBoneIndexes.Intersect(boneIndexes).Count() != boneIndexes.Count())
                {
                    return boneWeight;
                }

                // bindposeの計算
                Matrix4x4 headBoneBindpose = headBone.worldToLocalMatrix
                    * mesh.bindposes[boneWeight.boneIndex0]
                    * renderer.bones[boneWeight.boneIndex0].localToWorldMatrix;
                int headBoneIndex;
                var headBoneIndexAndBindpose = headBoneIndicesAndBindposes
                    .FirstOrDefault(indexAndBindpose => indexAndBindpose.bindpose == headBoneBindpose);
                if (headBoneIndexAndBindpose == null)
                {
                    headBoneIndex = renderer.bones.Length;
                    renderer.bones = renderer.bones.Concat(new[] { headBone }).ToArray();
                    mesh.bindposes = mesh.bindposes.Concat(new[] { headBoneBindpose }).ToArray();
                    headBoneIndicesAndBindposes.Add(new { index = headBoneIndex, bindpose = headBoneBindpose });
                }
                else
                {
                    headBoneIndex = headBoneIndexAndBindpose.index;
                }

                foreach (var eyeBoneIndex in eyeBoneIndexes)
                {
                    var index = boneIndexes.ToList().FindIndex(boneIndex => boneIndex == eyeBoneIndex);
                    switch (index)
                    {
                        case 0:
                            boneWeight.boneIndex1 = headBoneIndex;
                            boneWeight.weight1 = boneWeight.weight0 * headBoneWeight;
                            boneWeight.weight0 *= eyeBoneWeight;
                            break;
                        case 1:
                            boneWeight.boneIndex2 = headBoneIndex;
                            boneWeight.weight2 = boneWeight.weight1 * headBoneWeight;
                            boneWeight.weight1 *= eyeBoneWeight;
                            break;
                        case 2:
                            boneWeight.boneIndex3 = headBoneIndex;
                            boneWeight.weight3 = boneWeight.weight2 * headBoneWeight;
                            boneWeight.weight2 *= eyeBoneWeight;
                            break;
                    }
                }

                return boneWeight;
            }).ToArray();

            return true;
        }

        /// <summary>
        /// ダミー用の空のメッシュを生成します。
        /// </summary>
        /// <returns></returns>
        private static Mesh CreateDummyMesh()
        {
            var mesh = new Mesh()
            {
                name = "dummy-for-auto-eye-movement",
                vertices = new[] { new Vector3(0, 0, 0) }
            };
            return mesh;
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
        /// ふわふわのクラゲさんのツイート: “書き間違いだとした場合は沈み方にもよりますが、瞳メッシュの位置とボーンの回転軸の位置関係が近すぎることが原因と思われます。単なる幾何学的問題なのでこれを100さんが見落としてるというのは考えづらいですが。… ”
        /// <https://twitter.com/DD_JellyFish/status/1139051774352871424>
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="addedValueToArmature"></param>
        /// <param name="addedValueToShoulders"></param>
        /// <param name="addedValueToEyes"></param>
        private static void AddShouldersPositionYAndEyesPositionZ(
            GameObject avatar,
            float addedValueToArmature,
            float addedValueToShoulders,
            float addedValueToEyes
        )
        {
            if (addedValueToArmature == 0.0f && addedValueToShoulders == 0.0f && addedValueToEyes == 0.0f)
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

#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
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
                if (addedValueToEyes != 0.0f)
                {
                    foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye })
                    {
                        var humanName = bone.ToString();
                        var name = humanBones.Find(match: humanBone => humanBone.humanName == humanName).boneName;
                        humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == name)].position
                            += new Vector3(0, 0, addedValueToEyes);
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
