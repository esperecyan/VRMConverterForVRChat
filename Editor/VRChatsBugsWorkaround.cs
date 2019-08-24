using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using VRM;
using UniHumanoid;
using UniGLTF;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// VRChatの不具合などに対処します。
    /// </summary>
    public class VRChatsBugsWorkaround
    {
        /// <summary>
        /// VRChatのバグ対策用のシェーダー名に前置する文字列。
        /// </summary>
        public static readonly string ShaderNamePrefix = "VRChat/RenderQueueApplied/";

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

        /// <summary>
        /// オートアイムーブメントにおける目のボーンの回転角度の最大値。
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
        internal static readonly IEnumerable<HumanBodyBones> RequiredModifiedBonesForVRChat = new []{
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm
        };

        /// 『セシル変身アプリ』の目ボーンのパス。
        /// </summary>
        internal static readonly IDictionary<HumanBodyBones, string> CecilHenShinEyeBonePaths = new Dictionary<HumanBodyBones, string>() {
            { HumanBodyBones.LeftEye,  "Armature/Hips/Spine/Spine1/Spine2/Neck/Head/MeRoot/Me_L/LeftEyeRoot/LeftEye"   },
            { HumanBodyBones.RightEye, "Armature/Hips/Spine/Spine1/Spine2/Neck/Head/MeRoot/Me_R/RightEyeRoot/RightEye" },
        };

        /// <summary>
        /// VRChatで伏せ姿勢に使用するアニメーション名。
        /// </summary>
        private static readonly IEnumerable<string> ProneVRChatAnims = new[] { "PRONEIDLE", "PRONEFWD" };

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="addedShouldersPositionY">VRChat上でモデルがなで肩・いかり肩になる問題について、Shoulder/UpperArmボーンのPositionのYに加算する値。</param>
        /// <param name="addedArmaturePositionY"></param>
        /// <param name="fixProneAvatarPosition">伏せたときのアバターの位置が、自分視点と他者視点で異なるVRChatのバグに対処するなら <c>true</c>。</param>
        /// <param name="moveEyeBoneToFrontForEyeMovement"></param>
        /// <param name="forQuest"></param>
        /// <returns>変換中に発生したメッセージ。</returns>
        internal static IEnumerable<Converter.Message> Apply(
            GameObject avatar,
            bool enableAutoEyeMovement,
            float addedShouldersPositionY,
            bool fixProneAvatarPosition,
            float addedArmaturePositionY,
            float moveEyeBoneToFrontForEyeMovement,
            bool forQuest
        ) {
            var messages = new List<Converter.Message>();
            
            VRChatsBugsWorkaround.EnableAnimationOvrride(avatar: avatar);
            if (enableAutoEyeMovement)
            {
                VRChatsBugsWorkaround.SetEyeBonesForCecilHenShin(avatar: avatar);
            }
            if (enableAutoEyeMovement || forQuest)
            {
                VRChatsBugsWorkaround.EnableAutoEyeMovement(avatar: avatar);
            }
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
            VRChatsBugsWorkaround.AddShouldersPositionYAndEyesPositionZ(
                avatar: avatar,
                addedValueToArmature: addedArmaturePositionY,
                addedValueToShoulders: addedShouldersPositionY,
                addedValueToEyes: moveEyeBoneToFrontForEyeMovement
            );
            if (!forQuest)
            {
                IEnumerable<string> convertingFailedMaterialNames
                    = VRChatsBugsWorkaround.ApplyRenderQueues(avatar: avatar);
                if (convertingFailedMaterialNames.Count() > 0)
                {
                    messages.Add(new Converter.Message
                    {
                        message = string.Join(
                            separator: "\n• ",
                            value: new[] { Gettext._("Converting these materials (for VRChat Render Queue bug) was failed.") }.Concat(convertingFailedMaterialNames).ToArray()
                        ),
                        type = MessageType.Warning,
                    });
                }
            }
            if (fixProneAvatarPosition)
            {
                VRChatsBugsWorkaround.FixProneAvatarPosition(avatar: avatar);
            }

            return messages;
        }

        /// <summary>
        /// サブメッシュをRender Queueが小さい方から順に並べ替えます。
        /// </summary>
        /// <remarks>
        /// Furiaさんのツイート: “SubMeshの順序入れ替え、統合なんかをする感じのやつ。 自分用だけどとりあえず https://t.co/vRGUEnf9EV”
        /// <https://twitter.com/flammpfeil/status/1143160567848296449>
        /// SubMeshの順序入れ替え、統合なんかをする感じのやつ。　RenderQueueでソートもできる。
        /// <https://gist.github.com/flammpfeil/18bb0b5f41588c6530500375d1a273f6/fdb256524aa0c45f8bd07f03f931d3da9650ede4#file-submeshinspectorwindow-cs-L241-L269>
        /// 
        /// MIT License
        /// 
        /// Copyright(c) 2019 Furia
        /// Copyright(c) 2017-2018 Unity Technologies Japan
        /// 
        /// Permission is hereby granted, free of charge, to any person obtaining a copy
        /// of this software and associated documentation files(the "Software"), to deal
        /// in the Software without restriction, including without limitation the rights
        /// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        /// copies of the Software, and to permit persons to whom the Software is
        /// furnished to do so, subject to the following conditions:
        /// 
        /// The above copyright notice and this permission notice shall be included in all
        /// copies or substantial portions of the Software.
        /// 
        /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        /// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        /// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
        /// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        /// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        /// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        /// SOFTWARE.
        /// </remarks>
        /// <param name="renderer"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static GameObject[] SortSubMesh(Renderer renderer, Mesh target)
        {
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;

                var sortedMaterials = materials
                    .Select((x, i) => new { index = i, renderQueue = (x != null) ? x.renderQueue : 5001, material = x })
                    .OrderBy(x => x.renderQueue)
                    .Where(x => x.index < target.subMeshCount)
                    .ToArray();

                renderer.sharedMaterials = sortedMaterials.Select(x => x.material).ToArray();

                var sortedSubMesh = sortedMaterials
                    .Select(x => target.GetTriangles(x.index))
                    .ToArray();

                for (int i = 0; i < sortedSubMesh.Length; i++)
                {
                    target.SetTriangles(sortedSubMesh[i], i);
                }
            }

            //smr.materials = smr.materials.Take(Math.Min(smr.materials.Count() - 1, target.subMeshCount)).ToArray();


            return null;
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

            IEnumerable<BoneLimit> addedBoneLimits = VRChatUtility.RequiredHumanBodyBonesForAnimationOverride.Select(bones => {
                int missingHumanBodyBoneIndex = bones.ToList().FindIndex(match: bone => !existedHumanBodyBones.Contains(value: bone));
                if (missingHumanBodyBoneIndex == -1)
                {
                    return new BoneLimit[0];
                }
                
                Transform parent = avatar.GetComponent<Animator>().GetBoneTransform(humanBoneId: bones[missingHumanBodyBoneIndex - 1]);
                return bones.Skip(count: missingHumanBodyBoneIndex).Select(bone => {
                    Transform dummyBone = new GameObject(name: "vrc." + bone).transform;
                    dummyBone.parent = parent;
                    parent = dummyBone;
                    return new BoneLimit() { humanBone = bone, boneName = dummyBone.name };
                });
            }).ToList().SelectMany(boneLimit => boneLimit);

            if (addedBoneLimits.Count() == 0) {
                return;
            }
            
            avatarDescription.human = avatarDescription.human.Concat(addedBoneLimits).ToArray();
            ApplyAvatarDescription(avatar: avatar);
        }

        /// <summary>
        /// 『セシル変身アプリ』で出力されたモデルに<see cref="HumanBodyBones.LeftEye"/>、<see cref="HumanBodyBones.RightEye"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void SetEyeBonesForCecilHenShin(GameObject avatar)
        {
            AvatarDescription avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            List<BoneLimit> boneLimits = avatarDescription.human.ToList();

            var eyeHumanBones = new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye };

            foreach (HumanBodyBones humanBone in eyeHumanBones) {
                string path = VRChatsBugsWorkaround.CecilHenShinEyeBonePaths[humanBone];

                if (!string.IsNullOrEmpty(boneLimits.FirstOrDefault(predicate: boneLimit => boneLimit.humanBone == humanBone).boneName)
                    || !avatar.transform.Find(path))
                {
                    return;
                }

                boneLimits.Add(new BoneLimit {
                    humanBone = humanBone,
                    boneName = path.Split('/').Last(),
                });
            }

            avatarDescription.human = boneLimits.ToArray();
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
        ) {
            var humanoidDescription = avatar.GetComponent<VRMHumanoidDescription>();
            AvatarDescription avatarDescription = humanoidDescription.Description;
            HumanDescription humanDescription = avatarDescription.ToHumanDescription(root: avatar.transform);
            if (humanDescriptionModifier != null) {
                humanDescriptionModifier(humanDescription);
            }
            Avatar humanoidRig = AvatarBuilder.BuildHumanAvatar(go: avatar, humanDescription: humanDescription);
            humanoidRig.name = humanoidDescription.Avatar.name;
            EditorUtility.CopySerialized(humanoidRig, humanoidDescription.Avatar);
            PrefabUtility.ReplacePrefab(avatar, PrefabUtility.GetPrefabParent(avatar), ReplacePrefabOptions.ConnectToPrefab);
            EditorUtility.SetDirty(target: humanoidDescription.Avatar);
        }

        /// <summary>
        /// オートアイムーブメントが有効化される条件を揃えます。
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
            foreach (var path in VRChatUtility.RequiredPathForAutoEyeMovement.Concat(new string[] { VRChatUtility.AutoBlinkMeshPath })) {
                var current = avatar.transform;
                foreach (var name in path.Split(separator: '/')) {
                    Transform child = current.Find(name: name);
                    if (!child) {
                        child = new GameObject(name: name).transform;
                        child.parent = current;
                    }
                    current = child;
                }
            }

            // ダミーのまばたき用ブレンドシェイプの作成
            Mesh mesh = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetSharedMesh();
            if (mesh.blendShapeCount >= BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Count()) {
                return;
            }
            
            foreach (var name in BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Skip(count: mesh.blendShapeCount)) {
                BlendShapeReplacer.AddDummyShapeKey(mesh: mesh, name: name);
            }
            
            EditorUtility.SetDirty(target: mesh);
        }

        /// <summary>
        /// オートアイムーブメントが有効化される条件が揃っていれば、目ボーンの関連付けを外します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void DisableAutoEyeMovement(GameObject avatar)
        {
            var paths = VRChatUtility.RequiredPathForAutoEyeMovement.Concat(new string[] { VRChatUtility.AutoBlinkMeshPath });
            var transforms = paths.Concat(new string[] { VRChatUtility.AutoBlinkMeshPath }).Select(path => avatar.transform.Find(name: path));
            if (transforms.Contains(value: null))
            {
                return;
            }

            var renderer = avatar.transform.Find(name: VRChatUtility.AutoBlinkMeshPath).gameObject.GetOrAddComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;
            if (!mesh || mesh.blendShapeCount < BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Count())
            {
                return;
            }

            var eyeBones = new[] { HumanBodyBones.RightEye, HumanBodyBones.LeftEye }
                .Select(id => avatar.GetComponent<Animator>().GetBoneTransform(humanBoneId: id))
                .Where(bone => bone && transforms.Contains(value: bone));
            if (eyeBones.Count() == 0)
            {
                return;
            }
            
            AvatarDescription avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            var boneLimits = avatarDescription.human.ToList();
            foreach (Transform bone in eyeBones)
            {
                int index = boneLimits.FindIndex(match: limit => limit.boneName == bone.name);
                bone.name = bone.name.ToLower();
                BoneLimit boneLimit = boneLimits[index];
                boneLimit.boneName = bone.name;
                boneLimits[index] = boneLimit;
            }

            avatarDescription.human = boneLimits.ToArray();
            ApplyAvatarDescription(avatar: avatar);
        }

        /// <summary>
        /// オートアイムーブメントの目ボーンの角度を、<see cref="VRMLookAtBoneApplyer"/>で指定された角度のうち最小値になるようにウェイトペイントを行います。
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
                .Select(id => animator.GetBoneTransform(humanBoneId: id))
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

            float minDegree = new[] { lookAtBoneApplyer.HorizontalOuter, lookAtBoneApplyer.HorizontalInner, lookAtBoneApplyer.VerticalDown, lookAtBoneApplyer.VerticalUp }
                .Select(mapper => mapper.CurveYRangeDegree)
                .Min();
            float eyeBoneWeight = minDegree / VRChatsBugsWorkaround.MaxAutoEyeMovementDegree;
            float headBoneWeight = 1 - eyeBoneWeight;

            Transform headBone = avatar.GetComponent<VRMFirstPerson>().FirstPersonBone;
            var headBoneIndicesAndBindposes = boneIndicesAndBones[headBone]
                .Select(index => new { index, bindpose = mesh.bindposes[index] }).ToList();

            mesh.boneWeights = mesh.boneWeights.Select(boneWeight => {
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

                foreach (int eyeBoneIndex in eyeBoneIndexes)
                {
                    int index = boneIndexes.ToList().FindIndex(boneIndex => boneIndex == eyeBoneIndex);
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
            var mesh = new Mesh();
            mesh.name = "dummy-for-auto-eye-movement";
            mesh.vertices = new[] { new Vector3(0, 0, 0) };
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
        ) {
            if (addedValueToArmature == 0.0f && addedValueToShoulders == 0.0f && addedValueToEyes == 0.0f)
            {
                return;
            }

            ApplyAvatarDescription(avatar: avatar, humanDescriptionModifier: humanDescription => {
                List<HumanBone> humanBones = humanDescription.human.ToList();
                List<SkeletonBone> skeltonBones = humanDescription.skeleton.ToList();
                if (addedValueToArmature != 0.0f)
                {
                    var addedPosition = new Vector3(0, addedValueToArmature, 0);

                    string armatureName
                        = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).parent.name;
                    humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == armatureName)].position
                        += addedPosition;

                    avatar.GetComponent<VRC_AvatarDescriptor>().ViewPosition += addedPosition;
                }
                if (addedValueToShoulders != 0.0f)
                {
                    foreach (HumanBodyBones bone in VRChatsBugsWorkaround.RequiredModifiedBonesForVRChat)
                    {
                        var humanName = bone.ToString();
                        string name = humanBones.Find(match: humanBone => humanBone.humanName == humanName).boneName;
                        humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == name)].position
                            += new Vector3(0, addedValueToShoulders, 0);
                    }
                }
                if (addedValueToEyes != 0.0f)
                {
                    foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye })
                    {
                        var humanName = bone.ToString();
                        string name = humanBones.Find(match: humanBone => humanBone.humanName == humanName).boneName;
                        humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == name)].position
                            += new Vector3(0, 0, addedValueToEyes);
                    }
                }
            });
        }

        /// <summary>
        /// マテリアルのRender Queueが適用されないバグに対処します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <remarks>
        /// 参照:
        /// Use with VRChat – Type74
        /// <http://type74.lsrv.jp/use-with-vrchat/>
        /// エイスーさんのツイート: “メニューのアバタープレビューを見るとFade、Transparentを使用したメッシュの表示がおかしくなる問題がようやく解決しました。透過させるマテリアルが画像箇所のMaterials Elementの最後（下側）の方に来るようBlenderとかで並び順を変更するといいみたいですね。#VRChat… https://t.co/d1K5QYh0Gw”
        /// <https://twitter.com/Eisue_/status/1139460675305000961>
        /// </remarks>
        /// <retunrs>シェーダーの変換に失敗したマテリアル名を返します。</retunrs>
        private static IEnumerable<string> ApplyRenderQueues(GameObject avatar)
        {
            var convertingFailedMaterialNames = new List<string>();

            var alreadyGeneratedShaders = new Dictionary<string, Shader>();

            var namePattern = new Regex(
                pattern: @"(?<leading>Shader\s*"")(?<name>[^""]+)(?<following>""\s*{)",
                options: RegexOptions.IgnoreCase
            );
            var tagsPattern = new Regex(
                pattern: @"SubShader\s*{\s*Tags\s*{(?<tags>(?:\s*""(?<name>[^""]+)""\s*=\s*""(?<value>[^""]+)""\s*)+)}",
                options: RegexOptions.IgnoreCase
            );
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                VRChatsBugsWorkaround.SortSubMesh(renderer: renderer, target: renderer.sharedMesh);
                EditorUtility.SetDirty(renderer.sharedMesh);

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (!material || material.renderQueue == material.shader.renderQueue)
                    {
                        continue;
                    }

                    string queueTag = VRChatsBugsWorkaround.ConvertToQueueTag(renderQueue: material.renderQueue);
                    string shaderName = material.shader.name;
                    if (shaderName.StartsWith("VRChat/"))
                    {
                        shaderName = shaderName.Replace(oldValue: "VRChat/", newValue: "");
                    }
                    shaderName = VRChatsBugsWorkaround.ShaderNamePrefix + shaderName + "-" + queueTag;
                    if (alreadyGeneratedShaders.ContainsKey(shaderName))
                    {
                        Shader shader = alreadyGeneratedShaders[shaderName];
                        if (shader)
                        {
                            material.shader = shader;
                        }
                        else if (!convertingFailedMaterialNames.Contains(material.name))
                        {
                            convertingFailedMaterialNames.Add(material.name);
                        }
                        continue;
                    }

                    var sourceShaderUnityPath = UnityPath.FromAsset(material.shader);
                    string sourceShaderFullPath = sourceShaderUnityPath.FullPath;
                    if (!File.Exists(path: sourceShaderFullPath))
                    {
                        alreadyGeneratedShaders[shaderName] = null;
                        convertingFailedMaterialNames.Add(material.name);
                        continue;
                    }

                    string shaderContent = File.ReadAllText(path: sourceShaderFullPath, encoding: Encoding.UTF8);
                    Match match = tagsPattern.Match(input: shaderContent);
                    if (!match.Success)
                    {
                        alreadyGeneratedShaders[shaderName] = null;
                        convertingFailedMaterialNames.Add(material.name);
                        continue;
                    }

                    int index = Array.FindIndex(
                        array: match.Groups["name"].Captures.Cast<Capture>().ToArray(),
                        match: name => name.Value == "Queue"
                    );
                    if (index == -1)
                    {
                        int tagsContentEndIndex = match.Groups["tags"].Index + match.Groups["tags"].Length;
                        shaderContent = shaderContent.Substring(startIndex: 0, length: tagsContentEndIndex)
                            + " \"Queue\" = \"" + queueTag + "\""
                            + shaderContent.Substring(startIndex: tagsContentEndIndex);
                    }
                    else
                    {
                        Capture queueTagValue = match.Groups["value"].Captures[index];
                        shaderContent = shaderContent.Substring(startIndex: 0, length: queueTagValue.Index)
                            + queueTag
                            + shaderContent.Substring(startIndex: queueTagValue.Index + queueTagValue.Length);
                    }

                    string newNameShaderContent = namePattern.Replace(
                        input: shaderContent,
                        replacement: "${leading}" + shaderName.Replace(oldValue: "$", newValue: "$$") + "${following}",
                        count: 1
                    );
                    if (newNameShaderContent == shaderContent)
                    {
                        alreadyGeneratedShaders[shaderName] = null;
                        convertingFailedMaterialNames.Add(material.name);
                        continue;
                    }
                    
                    var destinationUnityPath = sourceShaderUnityPath.Parent
                        .Child(sourceShaderUnityPath.FileNameWithoutExtension + "-" + queueTag + sourceShaderUnityPath.Extension);
                    File.WriteAllText(
                        path: destinationUnityPath.FullPath,
                        contents: newNameShaderContent,
                        encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                    );

                    AssetDatabase.ImportAsset(destinationUnityPath.Value);
                    material.shader = AssetDatabase.LoadAssetAtPath<Shader>(destinationUnityPath.Value);
                    alreadyGeneratedShaders[material.shader.name] = material.shader;
                }
            }

            return convertingFailedMaterialNames;
        }

        /// <summary>
        /// 指定されたRender Queueに対応するSubShaderのQueueタグの値を返します。
        /// </summary>
        /// <param name="renderQueue"></param>
        /// <returns></returns>
        private static string ConvertToQueueTag(int renderQueue)
        {
            RenderQueue definedRenderQueue = new[] { RenderQueue.Transparent, RenderQueue.AlphaTest, RenderQueue.Geometry }
                .FirstOrDefault(value => (int)value <= renderQueue);

            if (definedRenderQueue == default(RenderQueue))
            {
                return renderQueue.ToString();
            }
            else if ((int)definedRenderQueue == renderQueue)
            {
                return definedRenderQueue.ToString();
            }
            else
            {
                return definedRenderQueue + "+" + (renderQueue - (int)definedRenderQueue);
            }
        }

        /// <summary>
        /// 伏せ姿勢のときに、アバターの位置が自分視点と他者視点でズレるバグについて、位置を補正します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <remarks>
        /// 参照:
        /// Fix the prone animation head position | Bug Reports | VRChat
        /// <https://vrchat.canny.io/bug-reports/p/fix-the-prone-animation-head-position>
        /// Sigさんのツイート: “VRChatにて、フルボディトラッキングじゃないけど寝たい！って人向けのモーションをアップデート。腕もある程度動かせます 画像を参考に導入し、あとはリアルの床に寝るだけ。 VR睡眠の沼に落ちよう！ ・目線のずれを若干修正 ・体や手の指が微妙に揺れるように https://t.co/DDEoOQNLnk … #VRChat… https://t.co/Cd0QKipSO7”
        /// <https://twitter.com/sleepyslowsheep/status/1035007669537406977>
        /// </remarks>
        private static void FixProneAvatarPosition(GameObject avatar)
        {
            VRChatUtility.AddCustomAnims(avatar: avatar);

            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();

            Vector3 gap = avatarDescriptor.ViewPosition
                - avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
            float zGap = gap.y - gap.z;

            foreach (string anim in VRChatsBugsWorkaround.ProneVRChatAnims)
            {
                AnimationClip clip = Duplicator.DuplicateAssetToFolder<AnimationClip>(
                    source: UnityPath.FromUnityPath(Converter.RootFolderPath).Child("Editor").Child(anim + ".anim")
                        .LoadAsset<AnimationClip>(),
                    prefabInstance: avatar,
                    fileName: anim + "-position-fixed.anim"
                );

                var curve = new AnimationCurve();
                curve.AddKey(time: 0, value: -zGap);
                curve.AddKey(time: clip.length, value: -zGap);
                clip.SetCurve(relativePath: "", type: typeof(Animator), propertyName: "RootT.z", curve: curve);

                avatarDescriptor.CustomStandingAnims[anim] = clip;
            }
        }
    }
}
