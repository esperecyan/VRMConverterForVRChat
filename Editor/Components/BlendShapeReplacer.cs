using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRM;
using UniGLTF;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#elif VRC_SDK_VRCSDK3
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
#endif

namespace Esperecyan.Unity.VRMConverterForVRChat.Components
{
    /// <summary>
    /// ブレンドシェイプに関する設定。
    /// </summary>
    internal class BlendShapeReplacer
    {
        /// <summary>
        /// Cats Blender PluginでVRChat用に生成されるまばたきのシェイプキー名。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// cats-blender-plugin/eyetracking.py at 0.13.3 · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/0.13.3/tools/eyetracking.py>
        /// </remarks>
        internal static readonly IEnumerable<string> OrderedBlinkGeneratedByCatsBlenderPlugin
            = new string[] { "vrc.blink_left", "vrc.blink_right", "vrc.lowerlid_left", "vrc.lowerlid_right" };

        /// <summary>
        /// まばたき用に生成するシェイプキー名。
        /// </summary>
        private static readonly string BlinkShapeKeyName = "vrc.blink";

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
        /// 表情用のテンプレートAnimatorControllerのGUID。
        /// </summary>
        private static readonly string FXTemplateGUID = "4dab2bc02bfaabc4faea4c6b4d8a142b";

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
        /// Unityにおけるブレンドシェイプフレームのウェイトの最高値。
        /// </summary>
        private static readonly float MaxBlendShapeFrameWeight = 100;

        /// <summary>
        /// まばたきの間隔。キーに秒、値にブレンドシェイプのウェイト (1が閉眼)。
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
        /// <see cref="BlendShapeReplacer.BlinkWeights">を基にした<see cref="BlendShapePreset.Neutral"/>の適用間隔。
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
        /// <see cref="BlendShapePreset.Neutral"/>と<see cref="BlendShapePreset.Blink"/>が同じキーを参照しているときの適用間隔。
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
        /// ハンドサイン用にシェイプキーを複製したときに前置する文字列。
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
        internal static IEnumerable<Converter.Message> Apply(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useAnimatorForBlinks,
            bool useShapeKeyNormalsAndTangents,
            VRMBlendShapeClip vrmBlendShapeForFINGERPOINT
        ) {
            var messages = new List<Converter.Message>();

            SetLipSync(avatar, clips, useShapeKeyNormalsAndTangents);

#if VRC_SDK_VRCSDK2
            if (useAnimatorForBlinks)
            {
                SetNeutralAndBlink(avatar, clips, useShapeKeyNormalsAndTangents);
            }
            else
            {
                SetBlinkWithoutAnimator(avatar, clips, useShapeKeyNormalsAndTangents);
                SetNeutralWithoutAnimator(avatar: avatar, clips: clips);
            }
#else
            SetNeutralWithoutAnimator(avatar, clips);
            EnableEyeLook(avatar, clips, useShapeKeyNormalsAndTangents);
#endif

            SetFeelings(avatar, clips, vrmBlendShapeForFINGERPOINT);

            return messages;
        }

        /// <summary>
        /// ダミーのシェイプキーを作成します。
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

            IEnumerable<BlendShape> shapeKeys = BlendShapeReplacer.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);

            foreach (var (newName, values) in BlendShapeReplacer.VisemeShapeKeyNamesAndValues)
            {
                if (mesh.GetBlendShapeIndex(newName) != -1)
                {
                    continue;
                }

                Vector3[] deltaVertices = null;
                foreach (Vector3[] vertices in values.SelectMany(presetAndWeight =>
                    clips.First(clip => clip.Preset == presetAndWeight.Key).ShapeKeyValues.Select(
                        shapeKeyNameAndWeight => shapeKeys
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

#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            avatarDescriptor.VisemeSkinnedMesh = renderer;
            avatarDescriptor.VisemeBlendShapes
                = BlendShapeReplacer.VisemeShapeKeyNamesAndValues.Select(nameAndValues => nameAndValues.Key).ToArray();
#endif
        }

        /// <summary>
        /// <see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>を変換します。
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
                            float blinkShapeKeyWeight = blinkClip.ShapeKeyValues[shapeKeyName];
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
            IEnumerable<BlendShape> shapeKeys = BlendShapeReplacer.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);
            mesh.ClearBlendShapes();
            foreach (string name in BlendShapeReplacer.OrderedBlinkGeneratedByCatsBlenderPlugin)
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
        /// <param name="shapeKeys"><see cref="BlendShapeReplacer.GetAllShapeKeys"/>の戻り値。</param>
        /// <returns></returns>
        private static Vector3[] GenerateShapeKey(
            IDictionary<string, float> namesAndWeights,
            IEnumerable<BlendShape> shapeKeys

        ) {
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
        /// Animatorコンポーネントを使用せずに、<see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>を変換します。
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

            IEnumerable<BlendShape> shapeKeys = BlendShapeReplacer.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents);
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
            foreach (string name in dummyShapeKeyNames)
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
        /// Animatorコンポーネントを使用せずに<see cref="BlendShapePreset.Neutral"/>を変換します。
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
            if (clip)
            {
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.BlinkShapeKeyName,
                    1,
                    BlendShapeReplacer.GenerateShapeKey(
                        clip.ShapeKeyValues,
                        BlendShapeReplacer.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents)
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
                settings.eyesLookingUp = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(x: -lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                    right = Quaternion.Euler(x: -lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                };
                settings.eyesLookingDown = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(x: lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                    right = Quaternion.Euler(x: lookAtBoneApplyer.VerticalUp.CurveYRangeDegree, 0, 0),
                };
                settings.eyesLookingLeft = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(0, y: -lookAtBoneApplyer.HorizontalOuter.CurveYRangeDegree, 0),
                    right = Quaternion.Euler(0, y: -lookAtBoneApplyer.HorizontalInner.CurveYRangeDegree, 0),
                };
                settings.eyesLookingRight = new VRCAvatarDescriptor.CustomEyeLookSettings.EyeRotations()
                {
                    left = Quaternion.Euler(0, y: lookAtBoneApplyer.HorizontalInner.CurveYRangeDegree, 0),
                    right = Quaternion.Euler(0, y: lookAtBoneApplyer.HorizontalOuter.CurveYRangeDegree, 0),
                };
            }

            descriptor.customEyeLookSettings = settings;
#endif
        }

        /// <summary>
        /// 指定したメッシュのすべてのシェイプキーを取得します。
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        /// <returns></returns>
        private static IEnumerable<BlendShape> GetAllShapeKeys(Mesh mesh, bool useShapeKeyNormalsAndTangents)
        {
            var shapeKeys = new List<BlendShape>();

            int meshVertexCount = mesh.vertexCount;
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var deltaVertices = new Vector3[meshVertexCount];
                var deltaNormals = new Vector3[meshVertexCount];
                var deltaTangents = new Vector3[meshVertexCount];

                mesh.GetBlendShapeFrameVertices(
                    i,
                    0,
                    deltaVertices,
                    useShapeKeyNormalsAndTangents ? deltaNormals : null,
                    useShapeKeyNormalsAndTangents ? deltaTangents : null
                );

                shapeKeys.Add(new BlendShape(name: mesh.GetBlendShapeName(i)) {
                    Positions = deltaVertices.ToList(),
                    Normals = deltaNormals.ToList(),
                    Tangents = deltaTangents.ToList(),
                });
            }

            return shapeKeys;
        }

        /// <summary>
        /// 単一アニメーションループ用に、空のコントローラーとアニメーションクリップを作成します。
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

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (controller && clip)
            {
                clip.ClearCurves();
                return controller;
            }

            AssetDatabase.MoveAssetToTrash(controllerPath);
            AssetDatabase.MoveAssetToTrash(clipPath);

            clip = new AnimationClip();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, clipPath);

            return AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
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
        ) {
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
        ) {
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

            var states = fxController.layers[1].stateMachine.states.Select(childState => childState.state).ToList();
#endif

            foreach (var preset in BlendShapeReplacer.MappingBlendShapeToVRChatAnim.Keys)
            {
                VRMBlendShapeClip blendShapeClip = preset == BlendShapePreset.Unknown
                    ? vrmBlendShapeForFINGERPOINT
                    : clips.FirstOrDefault(c => c.Preset == preset);
                if (!blendShapeClip)
                {
                    continue;
                }

                AnimationClip animationClip = CreateFeeling(avatar, blendShapeClip, ref clips);
                string anim = BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset].ToString();
#if VRC_SDK_VRCSDK2
                avatarDescriptor.CustomStandingAnims[anim] = animationClip;
                avatarDescriptor.CustomSittingAnims[anim] = animationClip;
#elif VRC_SDK_VRCSDK3
                states.First(s => s.name.ToLower() == anim.ToLower()).motion = animationClip;
#endif
            }
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
#if VRC_SDK_VRCSDK2
            var anim = Duplicator.DuplicateAssetToFolder<AnimationClip>(
                source: UnityPath.FromUnityPath(Converter.RootFolderPath).Child("animations")
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
#else
            var anim = new AnimationClip();
            AssetDatabase.CreateAsset(
                anim,
                Duplicator.DetermineAssetPath(prefabInstance: avatar, typeof(AnimationClip), fileName)
            );
            clips = BlendShapeReplacer.DuplicateShapeKeyToUnique(avatar, clip, clips);
#endif

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
        /// ブレンドシェイプ名の一覧を取得します。
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
        /// 指定されたブレンドシェイプに、<see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>に含まれるシェイプキーと
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
            string[] neutralAndBlinkShapeKeyNames = new[] { BlendShapePreset.Neutral, BlendShapePreset.Blink }
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
