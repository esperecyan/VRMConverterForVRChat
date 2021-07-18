using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRM;
using UniGLTF;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using SkinnedMeshUtility = Esperecyan.Unity.VRMConverterForVRChat.Utilities.SkinnedMeshUtility;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// ブレンドシェイプに関する設定。
    /// </summary>
    internal class BlendShapeReplacer
    {
        /// <summary>
        /// Unityにおけるブレンドシェイプフレームのウェイトの最高値。
        /// </summary>
        internal static readonly float MaxBlendShapeFrameWeight = 100;

        /// <summary>
        /// 【SDK2】Cats Blender PluginでVRChat用に生成されるまばたきのシェイプキー名。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// cats-blender-plugin/eyetracking.py at 0.13.3 · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/0.13.3/tools/eyetracking.py>
        /// </remarks>
        internal static readonly IEnumerable<string> OrderedBlinkGeneratedByCatsBlenderPlugin
            = new string[] { "vrc.blink_left", "vrc.blink_right", "vrc.lowerlid_left", "vrc.lowerlid_right" };

        /// <summary>
        /// 【SDK3】まばたき用に生成するシェイプキー名。
        /// </summary>
        private static readonly string BlinkShapeKeyName = "eyes_closed";

        /// <summary>
        /// 表情の変更に関するVIVEタッチパッド上の位置について、バーチャルキャストとVRchatの対応関係。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// おぐら@VDRAW ver1.2.1公開さんのツイート: “Vキャスでの表情の変え方について。VRMファイルで設定した「喜怒哀楽」はこのように割り振られているみたいです。 Unityで設定する時のBlendShapeのClip名は 喜：JOY　怒：ANGRY　哀：SORROW　楽：FUN　です。 #バーチャルキャスト #VirtualCast… https://t.co/pYgHLoelG2”
        /// <https://twitter.com/ogog_ogura/status/987522017678114816>
        /// アニメーションオーバーライドで表情をつけよう — VRで美少女になりたい人の備忘録
        /// <http://shiasakura.hatenablog.com/entry/2018/03/30/190811#%E3%83%A2%E3%83%BC%E3%82%B7%E3%83%A7%E3%83%B3%E3%82%92%E3%82%B3%E3%83%B3%E3%83%88%E3%83%AD%E3%83%BC%E3%83%A9%E3%83%BC%E3%81%A8%E5%AF%BE%E5%BF%9C%E3%81%95%E3%81%9B%E3%82%8B>
        /// </remarks>
        private static readonly Dictionary<BlendShapePreset, VRChatUtility.Anim> MappingBlendShapeToVRChatAnim = new Dictionary<BlendShapePreset, VRChatUtility.Anim> {
            { BlendShapePreset.Joy, VRChatUtility.Anim.VICTORY },
            { BlendShapePreset.Angry, VRChatUtility.Anim.HANDGUN },
            { BlendShapePreset.Sorrow, VRChatUtility.Anim.THUMBSUP },
            { BlendShapePreset.Fun, VRChatUtility.Anim.ROCKNROLL },
            { BlendShapePreset.Unknown, VRChatUtility.Anim.FINGERPOINT },
        };

        /// <summary>
        /// 【SDK3】各表情の上から時計回りの位置。
        /// </summary>
        private static readonly IList<BlendShapePreset> FacialExpressionsOrder = new[]
        {
            BlendShapePreset.Sorrow,
            BlendShapePreset.Joy,
            BlendShapePreset.Fun,
            BlendShapePreset.Angry,
        }.ToList();

        /// <summary>
        /// 【SDK3】表情用のテンプレートAnimatorControllerのGUID。
        /// </summary>
        private static readonly string FXTemplateGUID = "4dab2bc02bfaabc4faea4c6b4d8a142b";

        /// <summary>
        /// 【SDK3】表情用の<see cref="VRCExpressionsMenu"/>のGUID。
        /// </summary>
        private static readonly string VRCExpressionsMenuGUID = "91a5a0002bd103f448d572330b087f57";

        /// <summary>
        /// 【SDK3】デフォルトの<see cref="VRCExpressionsMenu"/>を再現したアセットのGUID。
        /// </summary>
        private static readonly string VRCEmoteGUID = "b824836cefba43040b1cfce3a0859812";

        /// <summary>
        /// 【SDK3】表情用の<see cref="VRCExpressionParameters"/>のGUID。
        /// </summary>
        private static readonly string VRCExpressionParametersGUID = "d492b41c65685944a96df77628e204bc";

        /// <summary>
        /// 【SDK2】ハンドサインアニメーションを格納するフォルダのGUID。
        /// </summary>
        private static readonly string HandSignAnimationsFolderGUID = "cdd041c20a1e5af4fb109fe3a08816ab";

        ///
        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.VisemeBlendShapes"/>に対応する、生成するシェイプキー名と生成するための値。
        /// </summary>
        /// <remarks>
        /// cats-blender-plugin/viseme.py at master · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/0.13.3/tools/viseme.py>
        /// 
        /// MIT License
        /// 
        /// Copyright (c) 2017 GiveMeAllYourCats
        /// 
        /// Permission is hereby granted, free of charge, to any person obtaining a copy
        /// of this software and associated documentation files (the 'Software'), to deal
        /// in the Software without restriction, including without limitation the rights
        /// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        /// copies of the Software, and to permit persons to whom the Software is
        /// furnished to do so, subject to the following conditions:
        /// 
        /// The above copyright notice and this permission notice shall be included in
        /// all copies or substantial portions of the Software.
        /// 
        /// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        /// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        /// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        /// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        /// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        /// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        /// SOFTWARE.
        /// 
        /// Code author: GiveMeAllYourCats
        /// Repo: https://github.com/michaeldegroot/cats-blender-plugin
        /// Edits by: GiveMeAllYourCats, Hotox
        /// </remarks>
        private static readonly KeyValuePair<string, IDictionary<BlendShapePreset, float>>[] VisemeShapeKeyNamesAndValues = {
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_sil",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_pp",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.0004f }, { BlendShapePreset.O, 0.0004f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_ff",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.2f }, { BlendShapePreset.I, 0.4f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_th",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.4f }, { BlendShapePreset.O, 0.15f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_dd",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.3f }, { BlendShapePreset.I, 0.7f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_kk",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.7f }, { BlendShapePreset.I, 0.4f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_ch",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.I, 0.9996f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_ss",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.I, 0.8f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_nn",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.2f }, { BlendShapePreset.I, 0.7f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_rr",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.I, 0.5f }, { BlendShapePreset.O, 0.3f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_aa",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.9998f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_e",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.I, 0.7f }, { BlendShapePreset.O, 0.3f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_ih",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.5f }, { BlendShapePreset.I, 0.2f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_oh",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.A, 0.2f }, { BlendShapePreset.O, 0.8f } }
            ),
            new KeyValuePair<string, IDictionary<BlendShapePreset, float>>(
                "vrc.v_ou",
                new Dictionary<BlendShapePreset, float>(){ { BlendShapePreset.O, 0.9994f } }
            ),
        };

        /// <summary>
        /// 【SDK2】まばたきの間隔。キーに秒、値にブレンドシェイプのウェイト (1が閉眼)。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// 自動まばたき — VRChat 技術メモ帳
        /// <https://vrcworld.wiki.fc2.com/wiki/%E8%87%AA%E5%8B%95%E3%81%BE%E3%81%B0%E3%81%9F%E3%81%8D>
        /// </remarks>
        private static readonly Dictionary<float, float> BlinkWeights = new Dictionary<float, float> {
            {  0.00f, 0 },

            {  3.50f, 0 },
            {  3.55f, 1 },
            {  3.65f, 0 },

            {  7.00f, 0 },
            {  7.05f, 1 },
            {  7.15f, 0 },

            { 11.70f, 0 },
            { 11.75f, 1 },
            { 11.85f, 0 },

            { 12.00f, 0 },
            { 12.05f, 1 },
            { 12.15f, 0 },
        };

        /// <summary>
        /// 【SDK2】<see cref="BlendShapeReplacer.BlinkWeights">を基にした<see cref="BlendShapePreset.Neutral"/>の適用間隔。
        /// キーに秒、値にブレンドシェイプのウェイト (1が適用状態)。
        /// </summary>
        private static readonly Dictionary<float, float> NeutralWeights = new Dictionary<float, float> {
            {  0.00f, 0 },
            {  1f / 60 * 3, 0 },
            {  1f / 60 * 4, 1 },

            {  3.50f, 1 },
            {  3.55f, 0 },
            {  3.65f, 1 },

            {  7.00f, 1 },
            {  7.05f, 0 },
            {  7.15f, 1 },

            { 11.70f, 1 },
            { 11.75f, 0 },
            { 11.85f, 1 },

            { 12.00f, 1 },
            { 12.05f, 0 },
            { 12.15f, 1 },
        };

        /// <summary>
        /// 【SDK2】<see cref="BlendShapePreset.Neutral"/>と<see cref="BlendShapePreset.Blink"/>が同じキーを参照しているときの適用間隔。
        /// キーに秒、値に適用対象のブレンドシェイプ。0の場合はいずれも適用しない。
        /// </summary>
        private static readonly Dictionary<float, BlendShapePreset> NeutralAndBlinkWeights
            = new Dictionary<float, BlendShapePreset> {
                {  0.00f, 0 },
                {  1f / 60 * 3, 0 },
                {  1f / 60 * 4, BlendShapePreset.Neutral },

                {  3.50f, BlendShapePreset.Neutral },
                {  3.55f, BlendShapePreset.Blink },
                {  3.65f, BlendShapePreset.Neutral },

                {  7.00f, BlendShapePreset.Neutral },
                {  7.05f, BlendShapePreset.Blink },
                {  7.15f, BlendShapePreset.Neutral },

                { 11.70f, BlendShapePreset.Neutral },
                { 11.75f, BlendShapePreset.Blink },
                { 11.85f, BlendShapePreset.Neutral },

                { 12.00f, BlendShapePreset.Neutral },
                { 12.05f, BlendShapePreset.Blink },
                { 12.15f, BlendShapePreset.Neutral },
            };

        /// <summary>
        /// <see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>干渉防止用のアニメーションの設定値。キーに秒、値に有効無効。
        /// </summary>
        private static readonly Dictionary<float, float> NeutralAndBlinkStopperWeights = new Dictionary<float, float> {
            {  0.00f, 0 },
            {  1f / 60 * 2, 0 },
            {  1f / 60 * 3, 1 },
        };

        /// <summary>
        /// 【SDK2】ハンドサイン用にシェイプキーを複製したときに前置する文字列。
        /// </summary>
        private static readonly string FeelingsShapeKeyPrefix = "vrc.feelings.";

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useAnimatorForBlinks"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        /// <param name="vrmBlendShapeForFINGERPOINT"></param>
        internal static void Apply(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useAnimatorForBlinks,
            bool useShapeKeyNormalsAndTangents,
            VRMBlendShapeClip vrmBlendShapeForFINGERPOINT
        )
        {
            SetLipSync(avatar, clips, useShapeKeyNormalsAndTangents);

            if (VRChatUtility.SDKVersion == 2)
            {
                if (useAnimatorForBlinks)
                {
                    SetNeutralAndBlink(avatar, clips, useShapeKeyNormalsAndTangents);
                }
                else
                {
                    SetBlinkWithoutAnimator(avatar, clips, useShapeKeyNormalsAndTangents);
                    SetNeutralWithoutAnimator(avatar: avatar, clips: clips);
                }
            }
            else
            {
                EnableEyeLook(avatar, clips, useShapeKeyNormalsAndTangents);
            }

            SetFeelings(avatar, clips, vrmBlendShapeForFINGERPOINT);
        }

        /// <summary>
        /// 【SDK2】ダミーのシェイプキーを作成します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="name"></param>
        internal static void AddDummyShapeKey(Mesh mesh, string name)
        {
            mesh.AddBlendShapeFrame(
                name,
                BlendShapeReplacer.MaxBlendShapeFrameWeight,
                new Vector3[mesh.vertexCount],
                null,
                null
            );
        }

        /// <summary>
        /// リップシンクの設定を行います。
        /// </summary>
        /// <remarks>
        /// <see cref="BlendShapePreset.A"/>、<see cref="BlendShapePreset.I"/>、<see cref="BlendShapePreset.O"/>が
        /// 単一のフレームを持つシェイプキーが存在しない場合、設定を行いません。
        /// 生成するシェイプキー名と同じシェイプキーが存在する場合、それを利用します。
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        private static void SetLipSync(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents
        )
        {
            Transform transform = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath);
            var renderer = transform.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;

            foreach (var preset in new[] { BlendShapePreset.A, BlendShapePreset.I, BlendShapePreset.O })
            {
                if (!clips.FirstOrDefault(c => c.Preset == preset))
                {
                    return;
                }
            }

            IEnumerable<BlendShape> shapeKeys = SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);

            foreach (var (newName, values) in BlendShapeReplacer.VisemeShapeKeyNamesAndValues)
            {
                if (mesh.GetBlendShapeIndex(newName) != -1)
                {
                    continue;
                }

                Vector3[] deltaVertices = null;
                foreach (Vector3[] vertices in values.SelectMany(presetAndWeight =>
                    BlendShapeReplacer.SubtractNeutralShapeKeyValues(clips.First(clip => clip.Preset == presetAndWeight.Key).ShapeKeyValues, clips)
                        .Select(shapeKeyNameAndWeight => shapeKeys
                            .First(shapeKey => shapeKey.Name == shapeKeyNameAndWeight.Key)
                            .Positions
                            .Select(vertix => vertix * (shapeKeyNameAndWeight.Value / VRMUtility.MaxBlendShapeBindingWeight * presetAndWeight.Value))
                            .ToArray()
                        )
                ))
                {
                    if (deltaVertices == null)
                    {
                        deltaVertices = vertices;
                        continue;
                    }

                    for (var i = 0; i < deltaVertices.Length; i++)
                    {
                        deltaVertices[i] += vertices[i];
                    }
                }

                mesh.AddBlendShapeFrame(
                    newName,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    deltaVertices,
                    null,
                    null
                );
            }
            EditorUtility.SetDirty(mesh);

            var avatarDescriptor
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                = avatar.GetComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
#else
                = (dynamic)null;
#endif
            avatarDescriptor.VisemeSkinnedMesh = renderer;
            avatarDescriptor.VisemeBlendShapes
                = BlendShapeReplacer.VisemeShapeKeyNamesAndValues.Select(nameAndValues => nameAndValues.Key).ToArray();
        }

        /// <summary>
        /// 【SDK2】<see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>を変換します。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// 最新版（10/02時点）自動まばたきの実装【VRChat技術情報】 — VRChatパブリックログ
        /// <https://jellyfish-qrage.hatenablog.com/entry/2018/10/02/152316>
        /// VRchatでMMDモデルをアバターとして使う方法——上級者編 — 東屋書店
        /// <http://www.hushimero.xyz/entry/vrchat-EyeTracking#%E5%A4%A7%E5%8F%A3%E9%96%8B%E3%81%91%E3%82%8B%E5%95%8F%E9%A1%8C%E3%81%AE%E8%A7%A3%E6%B1%BA>
        /// 技術勢の元怒さんのツイート: “自動まばたきはまばたきシェイプキーを、まばたき防止のほうは自動まばたきのエナブルONOFFを操作してますね。 欲しければサンプル渡せますよ。… ”
        /// <https://twitter.com/gend_VRchat/status/1100155987216879621>
        /// momoma/ナル@VRChatter/VTuberさんのツイート: “3F目にBehavior 1のキーを追加したら重複しなくなったわ、なるほどな… ”
        /// <https://twitter.com/momoma_creative/status/1137917887262339073>
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <paramref name="useShapeKeyNormalsAndTangents"/>
        private static void SetNeutralAndBlink(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents
        )
        {
            AnimatorController neutralAndBlinkController
                = BlendShapeReplacer.CreateSingleAnimatorController(avatar: avatar, name: "blink");
            AnimationClip animationClip = neutralAndBlinkController.animationClips[0];

            VRMBlendShapeClip blinkClip = null;
            foreach (var preset in new[] { BlendShapePreset.Blink, BlendShapePreset.Neutral })
            {
                VRMBlendShapeClip clip = clips.FirstOrDefault(c => c.Preset == preset);
                if (!clip)
                {
                    continue;
                }

                if (preset == BlendShapePreset.Blink)
                {
                    blinkClip = clip;
                }

                foreach (var (shapeKeyName, shapeKeyWeight) in clip.ShapeKeyValues)
                {
                    var keys = BlendShapeReplacer.BlinkWeights;
                    if (preset == BlendShapePreset.Neutral)
                    {
                        if (blinkClip && blinkClip.ShapeKeyValues.ContainsKey(shapeKeyName))
                        {
                            // NEUTRALとBlinkが同一のシェイプキーを参照していた場合
                            var blinkShapeKeyWeight = blinkClip.ShapeKeyValues[shapeKeyName];
                            var animationCurve = new AnimationCurve();
                            foreach (var (seconds, blendShapePreset) in BlendShapeReplacer.NeutralAndBlinkWeights)
                            {
                                float weight;
                                switch (blendShapePreset)
                                {
                                    case BlendShapePreset.Neutral:
                                        weight = shapeKeyWeight;
                                        break;
                                    case BlendShapePreset.Blink:
                                        weight = blinkShapeKeyWeight;
                                        break;
                                    default:
                                        weight = 0;
                                        break;
                                }
                                animationCurve.AddKey(new Keyframe(seconds, weight));
                            }

                            animationClip.SetCurve(
                                "",
                                typeof(SkinnedMeshRenderer),
                                "blendShape." + shapeKeyName,
                                animationCurve
                            );
                            continue;
                        }

                        keys = BlendShapeReplacer.NeutralWeights;
                    }

                    SetBlendShapeCurve(animationClip, shapeKeyName, shapeKeyWeight, keys, setRelativePath: false);
                }

                foreach (MaterialValueBinding binding in clip.MaterialValues)
                {

                    // TODO

                }
            }

            Transform transform = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath);
            transform.gameObject.AddComponent<Animator>().runtimeAnimatorController = neutralAndBlinkController;

            // VRChat側の自動まばたきを回避
            Mesh mesh = transform.GetSharedMesh();
            IEnumerable<BlendShape> shapeKeys = SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);
            mesh.ClearBlendShapes();
            foreach (var name in BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin)
            {
                BlendShapeReplacer.AddDummyShapeKey(mesh: mesh, name: name);
            }
            foreach (BlendShape shapeKey in shapeKeys)
            {
                if (BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Contains(shapeKey.Name))
                {
                    continue;
                }

                mesh.AddBlendShapeFrame(
                    shapeKey.Name,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    shapeKey.Positions.ToArray(),
                    shapeKey.Normals.ToArray(),
                    shapeKey.Tangents.ToArray()
                );
            }
            EditorUtility.SetDirty(mesh);
        }

        /// <summary>
        /// 指定されたシェイプキーを合成し、新しいシェイプキーを作成します。
        /// </summary>
        /// <param name="namesAndWeights">シェイプキー名と0〜100のウェイトの連想配列。</param>
        /// <param name="shapeKeys"><see cref="SkinnedMeshUtility.GetAllShapeKeys"/>の戻り値。</param>
        /// <returns></returns>
        private static Vector3[] GenerateShapeKey(
            IDictionary<string, float> namesAndWeights,
            IEnumerable<BlendShape> shapeKeys

        )
        {
            Vector3[] deltaVertices = null;
            foreach (var (name, weight) in namesAndWeights)
            {
                Vector3[] vertices = shapeKeys.First(shapeKey => shapeKey.Name == name).Positions.ToArray();
                if (deltaVertices == null)
                {
                    deltaVertices = new Vector3[vertices.Length];
                }

                for (var i = 0; i < deltaVertices.Length; i++)
                {
                    deltaVertices[i] += vertices[i] * (weight / VRMUtility.MaxBlendShapeBindingWeight);
                }
            }
            return deltaVertices;
        }

        /// <summary>
        /// 【SDK2】Animatorコンポーネントを使用せずに、<see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>を変換します。
        /// </summary>
        /// <remarks>
        /// <see cref="BlendShapePreset.Blink"/>が関連付けられたメッシュが見つからない、またはそのメッシュに
        /// <see cref="BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin"/>がそろっていれば何もしません。
        /// それらのキーが存在せず、<see cref="BlendShapePreset.Blink_L"/>、<see cref="BlendShapePreset.Blink_R"/>がいずれも設定されていればそれを優先します。
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        private static void SetBlinkWithoutAnimator(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents
        )
        {
            var renderer = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;

            if (BlendShapeReplacer.GetBlendShapeNames(mesh: mesh)
                .Take(BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Count())
                .SequenceEqual(BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin))
            {
                return;
            }

            IEnumerable<BlendShape> shapeKeys = SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);
            mesh.ClearBlendShapes();

            var dummyShapeKeyNames = new List<string>();
            if (clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Blink_L)
                && clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Blink_R))
            {
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.ElementAt(0),
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.GenerateShapeKey(
                        namesAndWeights: clips.First(c => c.Preset == BlendShapePreset.Blink_L).ShapeKeyValues,
                        shapeKeys: shapeKeys
                    ),
                    null,
                    null
                );
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.ElementAt(1),
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.GenerateShapeKey(
                        namesAndWeights: clips.First(c => c.Preset == BlendShapePreset.Blink_R).ShapeKeyValues,
                        shapeKeys: shapeKeys
                    ),
                    null,
                    null
                );
            }
            else
            {
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.ElementAt(0),
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.GenerateShapeKey(
                        namesAndWeights: clips.First(c => c.Preset == BlendShapePreset.Blink).ShapeKeyValues,
                        shapeKeys: shapeKeys
                    ),
                    null,
                    null
                );
                dummyShapeKeyNames.Add(BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.ElementAt(1));
            }
            dummyShapeKeyNames.AddRange(BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Skip(2));
            foreach (var name in dummyShapeKeyNames)
            {
                BlendShapeReplacer.AddDummyShapeKey(mesh: mesh, name: name);
            }

            foreach (BlendShape shapeKey in shapeKeys)
            {
                if (BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin.Contains(shapeKey.Name))
                {
                    continue;
                }

                mesh.AddBlendShapeFrame(
                    shapeKey.Name,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    shapeKey.Positions.ToArray(),
                    shapeKey.Normals.ToArray(),
                    shapeKey.Tangents.ToArray()
                );
            }

            EditorUtility.SetDirty(mesh);
        }

        /// <summary>
        /// 【SDK2】Animatorコンポーネントを使用せずに<see cref="BlendShapePreset.Neutral"/>を変換します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        private static void SetNeutralWithoutAnimator(GameObject avatar, IEnumerable<VRMBlendShapeClip> clips)
        {
            var clip = clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Neutral);
            if (!clip)
            {
                return;
            }

            var renderer = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = renderer.sharedMesh;
            foreach (var (name, weight) in clip.ShapeKeyValues)
            {
                renderer.SetBlendShapeWeight(mesh.GetBlendShapeIndex(name), weight);
            }
        }

        /// <summary>
        /// 指定した <see cref="VRMBlendShapeClip.ShapeKeyValues"> の値から、Neutralのキーを減算します。
        /// </summary>
        /// <param name="shapeKeyValues"></param>
        /// <param name="clips"></param>
        /// <returns></returns>
        private static IDictionary<string, float> SubtractNeutralShapeKeyValues(
            IDictionary<string, float> shapeKeyValues,
            IEnumerable<VRMBlendShapeClip> clips
        )
        {
            shapeKeyValues = new Dictionary<string, float>(shapeKeyValues);
            var neutralClip = clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Neutral);
            if (neutralClip != null)
            {
                foreach (var (name, weight) in neutralClip.ShapeKeyValues)
                {
                    if (!shapeKeyValues.ContainsKey(name))
                    {
                        shapeKeyValues[name] = 0;
                    }
                    shapeKeyValues[name] -= weight;
                }
            }
            return shapeKeyValues;
        }

        /// <summary>
        /// 【SDK3】<see cref="BlendShapePreset.Blink"/>を変換し、視線追従を有効化します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useShapeKeyNormalsAndTangents"/>
        private static void EnableEyeLook(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents
        )
        {
            VRMBlendShapeClip clip = clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Blink);
            var lookAtBoneApplyer = avatar.GetComponent<VRMLookAtBoneApplyer>();
            if (!clip && !lookAtBoneApplyer)
            {
                return;
            }

            var renderer = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer.sharedMesh;
            if (clip && mesh.GetBlendShapeIndex(BlendShapeReplacer.BlinkShapeKeyName) == -1)
            {
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.BlinkShapeKeyName,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.GenerateShapeKey(
                        BlendShapeReplacer.SubtractNeutralShapeKeyValues(clip.ShapeKeyValues, clips),
                        SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents)
                    ),
                    null,
                    null
                );
                EditorUtility.SetDirty(mesh);
            }

#if VRC_SDK_VRCSDK3
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            descriptor.enableEyeLook = true;

            var settings = new VRCAvatarDescriptor.CustomEyeLookSettings();

            if (clip)
            {
                settings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                settings.eyelidsSkinnedMesh = renderer;
                settings.eyelidsBlendshapes = new[] { mesh.blendShapeCount - 1, -1, -1 };
            }

            if (lookAtBoneApplyer)
            {
                settings.eyeMovement = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeMovements()
                {
                    excitement = 0.5f,
                    confidence = 0,
                };
                settings.leftEye = lookAtBoneApplyer.LeftEye.Transform;
                settings.rightEye = lookAtBoneApplyer.RightEye.Transform;
                settings.eyesLookingStraight = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.identity,
                    right = Quaternion.identity,
                };
                settings.eyesLookingUp = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(x: -lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                    right = Quaternion.Euler(x: -lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                };
                settings.eyesLookingDown = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(x: lookAtBoneApplyer.VerticalDown.CurveYRangeDegree, 0, 0),
                    right = Quaternion.Euler(x: lookAtBoneApplyer.VerticalDown.CurveYRangeDegree, 0, 0),
                };
                var horizontal = Math.Min(
                    lookAtBoneApplyer.HorizontalOuter.CurveYRangeDegree,
                    lookAtBoneApplyer.HorizontalInner.CurveYRangeDegree
                );
                settings.eyesLookingLeft = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(0, y: -horizontal, 0),
                    right = Quaternion.Euler(0, y: -horizontal, 0),
                };
                settings.eyesLookingRight = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(0, y: horizontal, 0),
                    right = Quaternion.Euler(0, y: horizontal, 0),
                };
            }

            descriptor.customEyeLookSettings = settings;
#endif
        }

        /// <summary>
        /// 【SDK2】単一アニメーションループ用に、空のコントローラーとアニメーションクリップを作成します。
        /// </summary>
        /// <remarks>
        /// 保存先にすでにアニメーションクリップが存在する場合、空にして返します。
        /// </remarks>
        /// <param name="avatar"></param>
        /// <param name="name"></param>
        private static AnimatorController CreateSingleAnimatorController(GameObject avatar, string name)
        {
            var path = Duplicator
                .DetermineAssetPath(prefabInstance: avatar, type: typeof(AnimatorController), fileName: name);
            var controllerPath = path + ".controller";
            var clipPath = path + ".anim";

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip)
            {
                clip.ClearCurves();
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller && clip)
            {
                return controller;
            }

            if (!clip)
            {
                clip = new AnimationClip();
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            if (controller)
            {
                controller.layers[0].stateMachine.states[0].state.motion = clip;
                return controller;
            }
            else
            {
                return AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            }
        }

        /// <summary>
        /// アニメーションクリップに、指定されたブレンドシェイプを追加します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="animationClip"></param>
        /// <param name="clip"></param>
        /// <param name="keys"></param>
        private static void SetBlendShapeCurves(
            GameObject avatar,
            AnimationClip animationClip,
            VRMBlendShapeClip clip,
            IDictionary<float, float> keys
        )
        {
            foreach (var (name, weight) in clip.ShapeKeyValues)
            {
                BlendShapeReplacer.SetBlendShapeCurve(animationClip, name, weight, keys, setRelativePath: true);
            }

            foreach (var bindings in clip.MaterialValues.GroupBy(binding => binding.MaterialName))
            {
                BlendShapeReplacer.SetBlendShapeCurve(animationClip, avatar, clip.BlendShapeName, bindings, keys.Keys);
            }
        }

        /// <summary>
        /// アニメーションクリップに、指定されたブレンドシェイプを追加します。
        /// </summary>
        /// <param name="animationClip"></param>
        /// <param name="shapeKeyName"></param>
        /// <param name="shapeKeyWeight">0〜100。</param>
        /// <param name="keys"></param>
        /// <param name="setRelativePath"></param>
        private static void SetBlendShapeCurve(
            AnimationClip animationClip,
            string shapeKeyName,
            float shapeKeyWeight,
            IDictionary<float, float> keys,
            bool setRelativePath
        )
        {
            var curve = new AnimationCurve();
            foreach (var (seconds, value) in keys)
            {
                curve.AddKey(new Keyframe(seconds, value * shapeKeyWeight));
            }

            animationClip.SetCurve(
                setRelativePath ? VRChatUtility.AutoBlinkMeshPath : "",
                typeof(SkinnedMeshRenderer),
                "blendShape." + shapeKeyName,
                curve
            );
        }

        /// <summary>
        /// アニメーションクリップに、指定されたマテリアルを追加します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="animationClip"></param>
        /// <param name="name"></param>
        /// <param name="binding"></param>
        /// <param name="secondsList"></param>
        private static void SetBlendShapeCurve(
            AnimationClip animationClip,
            GameObject avatar,
            string vrmBlendShapeName,
            IEnumerable<MaterialValueBinding> bindings,
            IEnumerable<float> secondsList
        )
        {
            var materials = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath)
                .GetComponent<SkinnedMeshRenderer>().sharedMaterials;

            var materialIndex = materials.ToList().FindIndex(m => m.name == bindings.First().MaterialName);

            var material = Duplicator.DuplicateAssetToFolder<Material>(
                source: materials[materialIndex],
                prefabInstance: avatar,
                fileName: $"{materials[materialIndex].name}-{vrmBlendShapeName}.mat"
            );

            VRMUtility.Bake(material, bindings);

            AnimationUtility.SetObjectReferenceCurve(
                animationClip,
                new EditorCurveBinding()
                {
                    path = VRChatUtility.AutoBlinkMeshPath,
                    type = typeof(SkinnedMeshRenderer),
                    propertyName = $"m_Materials.Array.data[{materialIndex}]",
                },
                secondsList
                    .Select(seconds => new ObjectReferenceKeyframe() { time = seconds, value = material }).ToArray()
            );
        }

        /// <summary>
        /// 手の形に喜怒哀楽を割り当てます。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="vrmBlendShapeForFINGERPOINT"></param>
        private static void SetFeelings(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            VRMBlendShapeClip vrmBlendShapeForFINGERPOINT
        )
        {
            var usedPresets = new List<BlendShapePreset>();
            var animationClips = new List<AnimationClip>();

#if VRC_SDK_VRCSDK2
            VRChatUtility.AddCustomAnims(avatar: avatar);

            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
#elif VRC_SDK_VRCSDK3
            var fxController = Duplicator.DuplicateAssetToFolder(
                source: AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    AssetDatabase.GUIDToAssetPath(BlendShapeReplacer.FXTemplateGUID)
                ),
                prefabInstance: avatar
            );

            var avatarDescriptor = avatar.GetOrAddComponent<VRCAvatarDescriptor>();
            avatarDescriptor.customizeAnimationLayers = true;
            avatarDescriptor.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Base,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Additive,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Gesture,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Action,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    animatorController = fxController,
                },
            };
            avatarDescriptor.specialAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Sitting,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.TPose,
                    isDefault = true,
                },
                new VRCAvatarDescriptor.CustomAnimLayer()
                {
                    type = VRCAvatarDescriptor.AnimLayerType.IKPose,
                    isDefault = true,
                },
            };

            var childStates = fxController.layers.First(layer => layer.name == "Feelings").stateMachine.states;

            avatarDescriptor.customExpressions = true;
            avatarDescriptor.expressionsMenu = Duplicator.DuplicateAssetToFolder(
                source: AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(
                    AssetDatabase.GUIDToAssetPath(BlendShapeReplacer.VRCExpressionsMenuGUID)
                ),
                prefabInstance: avatar
            );
            avatarDescriptor.expressionsMenu.controls.First(control => control.subMenu != null).subMenu
                = Duplicator.DuplicateAssetToFolder(
                    source: AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(
                        AssetDatabase.GUIDToAssetPath(BlendShapeReplacer.VRCEmoteGUID)
                    ),
                    prefabInstance: avatar
                );
            avatarDescriptor.expressionParameters = Duplicator.DuplicateAssetToFolder(
                source: AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(
                    AssetDatabase.GUIDToAssetPath(BlendShapeReplacer.VRCExpressionParametersGUID)
                ),
                prefabInstance: avatar
            );

            var blendTree
                = (BlendTree)childStates.First(childState => childState.state.name == "FaceBlend").state.motion;
            var motions = blendTree.children;

#endif
            AnimationClip neutral = null;

            foreach (var preset in BlendShapeReplacer.MappingBlendShapeToVRChatAnim.Keys.Concat(new[] { BlendShapePreset.Neutral }))
            {
                if (VRChatUtility.SDKVersion == 2 && preset == BlendShapePreset.Neutral)
                {
                    continue;
                }

                VRMBlendShapeClip blendShapeClip = preset == BlendShapePreset.Unknown
                    ? vrmBlendShapeForFINGERPOINT
                    : clips.FirstOrDefault(c => c.Preset == preset);
                if (!blendShapeClip)
                {
                    if (preset == BlendShapePreset.Neutral)
                    {
                        neutral = Duplicator.CreateObjectToFolder(
                            source: new AnimationClip(),
                            prefabInstance: avatar,
                            destinationFileName: "Neutral.anim"
                        );
                        animationClips.Add(neutral);
                    }
                    continue;
                }
                usedPresets.Add(preset);

                AnimationClip animationClip = CreateFeeling(avatar, blendShapeClip, ref clips);
                var anim = preset == BlendShapePreset.Neutral ? "Neutral" : BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset].ToString();
#if VRC_SDK_VRCSDK2
                avatarDescriptor.CustomStandingAnims[anim] = animationClip;
                avatarDescriptor.CustomSittingAnims[anim] = animationClip;
#elif VRC_SDK_VRCSDK3
                if (preset == BlendShapePreset.Neutral)
                {
                    neutral = animationClip;
                }
                else
                {
                    childStates.First(childState => childState.state.name.ToLower() == anim.ToLower()).state.motion
                        = animationClip;
                    if (preset != BlendShapePreset.Unknown)
                    {
                        // Expressionメニューによる表情変更
                        var index = BlendShapeReplacer.FacialExpressionsOrder.IndexOf(preset) + 1;
                        var motion = motions[index];
                        motion.motion = animationClip;
                        motions[index] = motion;
                    }
                }
                animationClips.Add(animationClip);
#endif
            }
#if VRC_SDK_VRCSDK3
            motions[0].motion = neutral;
            blendTree.children = motions;

            // Write Defaultsが無効でも動作するように、各アニメーションクリップへ初期値を追加
            var materials = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath)
                .GetComponent<SkinnedMeshRenderer>().sharedMaterials;

            foreach (var clip in usedPresets.Select(preset => clips.First(clip => preset == BlendShapePreset.Unknown
                ? clip.BlendShapeName == vrmBlendShapeForFINGERPOINT.BlendShapeName
                : clip.Preset == preset)))
            {
                foreach (var animationClip in animationClips)
                {
                    var alreadyExistingPropertyNames
                        = AnimationUtility.GetCurveBindings(animationClip).Select(binding => binding.propertyName);

                    foreach (var (name, weight) in clip.ShapeKeyValues)
                    {
                        if (alreadyExistingPropertyNames.Contains("blendShape." + name))
                        {
                            continue;
                        }
                        BlendShapeReplacer.SetBlendShapeCurve(
                            animationClip,
                            name,
                            weight,
                            keys: new Dictionary<float, float>() { { 0, 0 }, { animationClip.length, 0 } },
                            setRelativePath: true
                        );
                    }

                    foreach (var bindings in clip.MaterialValues.GroupBy(binding => binding.MaterialName))
                    {
                        var materialIndex = materials.ToList().FindIndex(m => m.name == bindings.First().MaterialName);
                        var propertyName = $"m_Materials.Array.data[{materialIndex}]";
                        if (alreadyExistingPropertyNames.Contains(propertyName))
                        {
                            continue;
                        }
                        AnimationUtility.SetObjectReferenceCurve(
                            animationClip,
                            new EditorCurveBinding()
                            {
                                path = VRChatUtility.AutoBlinkMeshPath,
                                type = typeof(SkinnedMeshRenderer),
                                propertyName = $"m_Materials.Array.data[{materialIndex}]",
                            },
                            new[] { new ObjectReferenceKeyframe() { time = 0, value = materials[materialIndex] } }
                        );
                    }
                }
            }

            foreach (var childState in childStates)
            {
                if (childState.state.motion != null)
                {
                    continue;
                }
                childState.state.motion = neutral;
            }

            // アニメーションクリップが設定されていないステートへ、空のアニメーションクリップを割り当て (Write Defaultsなしで正常に動作するように)
            var empty = Duplicator.CreateObjectToFolder(
                source: new AnimationClip(),
                prefabInstance: avatar,
                destinationFileName: "empty.anim"
            );
            foreach (var layer in fxController.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.motion != null)
                    {
                        continue;
                    }
                    state.state.motion = empty;
                }
            }
#endif
        }

        /// <summary>
        /// 表情の設定を行うアニメーションクリップを作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="vrmBlendShape"></param>
        /// <param name="clips"></param>
        /// <returns></returns>
        private static AnimationClip CreateFeeling(
            GameObject avatar,
            VRMBlendShapeClip clip,
            ref IEnumerable<VRMBlendShapeClip> clips
        )
        {
            var fileName = clip.Preset + ".anim";
            AnimationClip anim;
            if (VRChatUtility.SDKVersion == 2)
            {
                anim = Duplicator.DuplicateAssetToFolder<AnimationClip>(
                    source: UnityPath
                        .FromUnityPath(AssetDatabase.GUIDToAssetPath(BlendShapeReplacer.HandSignAnimationsFolderGUID))
                        .Child(BlendShapeReplacer.MappingBlendShapeToVRChatAnim[clip.Preset] + ".anim")
                        .LoadAsset<AnimationClip>(),
                    prefabInstance: avatar,
                    fileName
                );

                Transform transform = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath);
                if (transform.GetComponent<Animator>())
                {
                    var curve = new AnimationCurve();
                    foreach (var (seconds, value) in BlendShapeReplacer.NeutralAndBlinkStopperWeights)
                    {
                        curve.AddKey(new Keyframe(seconds, value));
                    }
                    anim.SetCurve(
                        VRChatUtility.AutoBlinkMeshPath,
                        typeof(Behaviour),
                        "m_Enabled",
                        curve
                    );

                    clips = BlendShapeReplacer.DuplicateShapeKeyToUnique(avatar, clip, clips);
                }
            }
            else
            {
                anim = Duplicator.CreateObjectToFolder(
                    source: new AnimationClip(),
                    prefabInstance: avatar,
                    destinationFileName: fileName
                );
            }

            SetBlendShapeCurves(
                avatar,
                animationClip: anim,
                clip: clip,
                keys: new Dictionary<float, float> {
                    { 0, 1 },
                    { anim.length, 1 },
                }
            );

            return anim;
        }

        /// <summary>
        /// 【SDK2】ブレンドシェイプ名の一覧を取得します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetBlendShapeNames(Mesh mesh)
        {
            var blendShapeNames = new List<string>();
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                blendShapeNames.Add(mesh.GetBlendShapeName(i));
            }
            return blendShapeNames;
        }

        /// <summary>
        /// 【SDK2】指定されたブレンドシェイプに、<see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>に含まれるシェイプキーと
        /// 同一のシェイプキーが含まれていれば、そのシェイプキーを複製します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clip"></param>
        /// <param name="clips"></param>
        /// <returns>置換後のVRMのブレンドシェイプ一覧。</returns>
        private static IEnumerable<VRMBlendShapeClip> DuplicateShapeKeyToUnique(
            GameObject avatar,
            VRMBlendShapeClip clip,
            IEnumerable<VRMBlendShapeClip> clips
        )
        {
            var neutralAndBlinkShapeKeyNames = new[] { BlendShapePreset.Neutral, BlendShapePreset.Blink }
                .Select(selector: blendShapePreset => clips.FirstOrDefault(c => c.Preset == blendShapePreset))
                .SelectMany(selector: blendShapeClip =>
                    blendShapeClip ? blendShapeClip.ShapeKeyValues : new Dictionary<string, float>())
                .Select(selector: nameAndWeight => nameAndWeight.Key)
                .ToArray();

            Mesh mesh = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetSharedMesh();
            foreach (var (oldName, weight) in clip.ShapeKeyValues)
            {
                if (!neutralAndBlinkShapeKeyNames.Contains(oldName))
                {
                    continue;
                }

                var newName = BlendShapeReplacer.FeelingsShapeKeyPrefix + oldName;
                var index = mesh.GetBlendShapeIndex(oldName);
                var frameCount = mesh.GetBlendShapeFrameCount(index);
                for (var i = 0; i < frameCount; i++)
                {
                    var deltaVertices = new Vector3[mesh.vertexCount];
                    var deltaNormals = new Vector3[mesh.vertexCount];
                    var deltaTangents = new Vector3[mesh.vertexCount];

                    mesh.GetBlendShapeFrameVertices(index, i, deltaVertices, deltaNormals, deltaTangents);

                    mesh.AddBlendShapeFrame(
                        newName,
                        mesh.GetBlendShapeFrameWeight(shapeIndex: index, frameIndex: i),
                        deltaVertices,
                        deltaNormals,
                        deltaTangents
                    );
                }

                EditorUtility.SetDirty(mesh);

                clips = VRMUtility.ReplaceShapeKeyName(clips, oldName, newName);
            }

            return clips;
        }
    }
}
