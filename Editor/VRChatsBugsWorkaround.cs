using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        /// Cats Blender PluginでVRChat用に生成されるまばたきのシェイプキー名。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// cats-blender-plugin/eyetracking.py at master · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/master/tools/eyetracking.py>
        /// </remarks>
        private static readonly string[] OrderedBlinkGeneratedByCatsBlenderPlugin = {
            "vrc.blink_left",
            "vrc.blink_right",
            "vrc.lowerlid_left",
            "vrc.lowerlid_right"
        };
        
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
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="enableAutoEyeMovement">オートアイムーブメントを有効化するなら<c>true</c>、無効化するなら<c>false</c>。</param>
        /// <param name="fixVRoidSlopingShoulders">VRoid Studioから出力されたモデルがなで肩になる問題について、ボーンのPositionを変更するなら<c>true</c>。</param>
        /// <param name="changeMaterialsForWorldsNotHavingDirectionalLight">Directional Lightがないワールド向けにマテリアルを変更するなら <c>true</c>。</param>
        internal static void Apply(
            GameObject avatar,
            bool enableAutoEyeMovement,
            bool fixVRoidSlopingShoulders,
            bool changeMaterialsForWorldsNotHavingDirectionalLight
        ) {
            VRChatsBugsWorkaround.AdjustHumanDescription(avatar: avatar);
            VRChatsBugsWorkaround.EnableAnimationOvrride(avatar: avatar);
            if (enableAutoEyeMovement)
            {
                VRChatsBugsWorkaround.EnableAutoEyeMovement(avatar: avatar);
                VRChatsBugsWorkaround.ApplyAutoEyeMovementDegreeMapping(avatar: avatar);
            }
            else {
                VRChatsBugsWorkaround.DisableAutoEyeMovement(avatar: avatar);
            }
            if (fixVRoidSlopingShoulders)
            {
                VRChatsBugsWorkaround.FixVRoidSlopingShoulders(avatar: avatar);
            }
            if (changeMaterialsForWorldsNotHavingDirectionalLight)
            {
                VRChatsBugsWorkaround.ChangeMaterialsForWorldsNotHavingDirectionalLight(avatar: avatar);
            }
        }

        /// <summary>
        /// <see cref="HumanBodyBones.UpperChest"/>が存在する場合、それを<see cref="HumanBodyBones.Chest"/>とし、元の<see cref="HumanBodyBones.Chest"/>の関連付けは外すようにした。
        /// </summary>
        /// <seealso cref="VRC_SdkControlPanel.AnalyzeIK"/>
        /// <param name="avatar"></param>
        private static void AdjustHumanDescription(GameObject avatar)
        {
            AvatarDescription avatarDescription = avatar.GetComponent<VRMHumanoidDescription>().Description;

            List<BoneLimit> boneLimits = avatarDescription.human.ToList();
            var upperChest = boneLimits.FirstOrDefault(predicate: boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest);
            if (string.IsNullOrEmpty(upperChest.boneName)) {
                return;
            }

            boneLimits.Remove(boneLimits.First(predicate: boneLimit => boneLimit.humanBone == HumanBodyBones.Chest));

            upperChest.humanBone = HumanBodyBones.Chest;
            boneLimits[boneLimits.FindIndex(boneLimit => boneLimit.humanBone == HumanBodyBones.UpperChest)] = upperChest;

            avatarDescription.human = boneLimits.ToArray();
            ApplyAvatarDescription(avatar: avatar);
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
            humanoidDescription.Avatar = humanoidRig;
            avatar.GetComponent<Animator>().avatar = humanoidRig;
            EditorUtility.SetDirty(target: humanoidRig);
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
            var renderer = avatar.transform.Find(name: VRChatUtility.AutoBlinkMeshPath).gameObject.GetOrAddComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;
            if (mesh && mesh.blendShapeCount >= VRChatsBugsWorkaround.OrderedBlinkGeneratedByCatsBlenderPlugin.Length) {
                return;
            }

            if (!mesh)
            {
                mesh = renderer.sharedMesh = VRChatsBugsWorkaround.CreateDummyMesh();
            }
            
            foreach (var name in VRChatsBugsWorkaround.OrderedBlinkGeneratedByCatsBlenderPlugin.Skip(count: mesh.blendShapeCount)) {
                VRChatsBugsWorkaround.CreateDummyBlendShape(mesh: mesh, name: name);
            }
            
            EditorUtility.SetDirty(target: mesh);
        }

        /// <summary>
        /// オートアイムーブメントが有効化される条件が揃っていれば、破壊します。
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
            if (!mesh || mesh.blendShapeCount < VRChatsBugsWorkaround.OrderedBlinkGeneratedByCatsBlenderPlugin.Length)
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
        private static void ApplyAutoEyeMovementDegreeMapping(GameObject avatar)
        {
            var lookAtBoneApplyer = avatar.GetComponent<VRMLookAtBoneApplyer>();
            if (!lookAtBoneApplyer)
            {
                return;
            }

            float minDegree = new[] { lookAtBoneApplyer.HorizontalOuter, lookAtBoneApplyer.HorizontalInner, lookAtBoneApplyer.VerticalDown, lookAtBoneApplyer.VerticalUp }
                .Select(mapper => mapper.CurveYRangeDegree)
                .Min();
            float eyeBoneWeight = minDegree / VRChatsBugsWorkaround.MaxAutoEyeMovementDegree;
            float headBoneWeight = 1 - eyeBoneWeight;

            Transform headBone = avatar.GetComponent<VRMFirstPerson>().FirstPersonBone;
            var eyeBones = new[] { HumanBodyBones.RightEye, HumanBodyBones.LeftEye }
                .Select(id => avatar.GetComponent<Animator>().GetBoneTransform(humanBoneId: id));

            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Transform[] bones = renderer.bones;
                IEnumerable<int> eyeBoneIndexes = eyeBones.Select(eyeBone => bones.IndexOf(target: eyeBone)).Where(index => index >= 0);
                if (eyeBoneIndexes.Count() == 0)
                {
                    continue;
                }

                Mesh mesh = renderer.sharedMesh;

                int headBoneIndex = bones.IndexOf(target: headBone);
                if (headBoneIndex < 0)
                {
                    renderer.bones = bones.Concat(new[] { headBone }).ToArray();
                    headBoneIndex = bones.Length;
                    mesh.bindposes = mesh.bindposes.Concat(new[] { headBone.worldToLocalMatrix }).ToArray();
                }

                mesh.boneWeights = mesh.boneWeights.Select(boneWeight => {
                    IEnumerable<float> weights = new[] { boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3 }.Where(weight => weight > 0);
                    IEnumerable<int> boneIndexes = new[] { boneWeight.boneIndex0, boneWeight.boneIndex1, boneWeight.boneIndex2, boneWeight.boneIndex3 }.Take(weights.Count());
                    if (eyeBoneIndexes.Intersect(boneIndexes).Count() == 0 || boneIndexes.Contains(headBoneIndex))
                    {
                        return boneWeight;
                    }

                    foreach (int eyeBoneIndex in eyeBoneIndexes)
                    {
                        int index = boneIndexes.ToList().FindIndex(boneIndex => boneIndex == eyeBoneIndex);
                        switch (index)
                        {
                            case 0:
                                boneWeight.weight0 = eyeBoneWeight;
                                boneWeight.boneIndex1 = headBoneIndex;
                                boneWeight.weight1 = headBoneWeight;
                                break;
                            case 1:
                                boneWeight.weight1 = eyeBoneWeight;
                                boneWeight.boneIndex2 = headBoneIndex;
                                boneWeight.weight2 = headBoneWeight;
                                break;
                            case 2:
                                boneWeight.weight2 = eyeBoneWeight;
                                boneWeight.boneIndex3 = headBoneIndex;
                                boneWeight.weight3 = headBoneWeight;
                                break;
                        }
                    }

                                return boneWeight;
                }).ToArray();
            }
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
        /// ダミーのブレンドシェイプを作成します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="name"></param>
        private static void CreateDummyBlendShape(Mesh mesh, string name)
        {
            mesh.AddBlendShapeFrame(
                shapeName: name,
                frameWeight: 0,
                deltaVertices: new Vector3[mesh.vertexCount],
                deltaNormals: new Vector3[mesh.vertexCount],
                deltaTangents: new Vector3[mesh.vertexCount]
            );
        }

        /// <summary>
        /// VRoid Studioから出力されたモデルがなで肩になる問題について、ボーンのPositionを変更します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void FixVRoidSlopingShoulders(GameObject avatar)
        {
            IDictionary<HumanBodyBones, string> bonesAndNames = avatar.GetComponent<VRMHumanoidDescription>().Description.human
                .ToDictionary(keySelector: boneLimit => boneLimit.humanBone, elementSelector: humanBone => humanBone.boneName);
            if (VRoidUtility.RequiredModifiedBonesAndNamesForVRChat.All(boneAndName => bonesAndNames.Contains(item: boneAndName)))
            {
                ApplyAvatarDescription(avatar: avatar, humanDescriptionModifier: humanDescription => {
                    List<SkeletonBone> skeltonBones = humanDescription.skeleton.ToList();
                    foreach (string name in VRoidUtility.RequiredModifiedBonesAndNamesForVRChat.Values)
                    {
                        humanDescription.skeleton[skeltonBones.FindIndex(match: skeltonBone => skeltonBone.name == name)].position
                            += VRoidUtility.AddedPositionValueForVRChat;
                    }
                });
            }
        }

        /// <summary>
        /// Directional Lightがないワールド向けに、マテリアルにMToonが設定されている場合、MToon-1.7へ変更します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <remarks>
        /// 参照:
        /// まじかる☆しげぽん@VRoidさんのツイート: “UniVRM0.49に含まれるMToonは、これまでDirectionalLightで暗くなってもキャラが暗くならなかったのが修正されたので、VRChatのようなDirectionalLightが無い環境だと逆にこういう風になってしまうっぽいです。#VRoid https://t.co/3OQ2uLvfOx”
        /// <https://twitter.com/m_sigepon/status/1091418527775391744>
        /// さんたーPさんのツイート: “僕としては VRChat に持っていくなら MToon for VRChat みたいな派生 MToon シェーダを作るのが最善かと思います。パラメータは使い回しで、DirectionalLight が～といった VRChat の特殊な状況に対応するための処理を入れた MToon を。… https://t.co/4AHjkaqxaY”
        /// <https://twitter.com/santarh/status/1088340412765356032>
        /// </remarks>
        private static void ChangeMaterialsForWorldsNotHavingDirectionalLight(GameObject avatar)
        {
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials) {
                    if (!material || material.shader.name != "VRM/MToon")
                    {
                        continue;
                    }

                    int renderQueue = material.renderQueue;
                    material.shader = Shader.Find("VRChat/MToon-1.7");
                    material.renderQueue = renderQueue;
                }
            }
        }
    }
}
