using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRM;
using UniGLTF;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// ブレンドシェイプに関する設定。
    /// </summary>
    public class BlendShapeReplacer
    {
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
        public static readonly Dictionary<BlendShapePreset, VRChatUtility.Anim> MappingBlendShapeToVRChatAnim = new Dictionary<BlendShapePreset, VRChatUtility.Anim> {
            { BlendShapePreset.Joy, VRChatUtility.Anim.VICTORY },
            { BlendShapePreset.Angry, VRChatUtility.Anim.HANDGUN },
            { BlendShapePreset.Sorrow, VRChatUtility.Anim.THUMBSUP },
            { BlendShapePreset.Fun, VRChatUtility.Anim.ROCKNROLL },
        };

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
        internal static void Apply(GameObject avatar)
        {
            var blendShapeProxy = avatar.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy)
            {
                return;
            }

            if (!blendShapeProxy.BlendShapeAvatar)
            {
                return;
            }

            SetLipSync(avatar: avatar);

            var relativePathToNeutralAndBlinkMesh = SetNeutralAndBlink(avatar: avatar);

            SetFeelings(avatar: avatar, relativePathToNeutralAndBlinkMesh: relativePathToNeutralAndBlinkMesh);
        }

        /// <summary>
        /// リップシンクの設定を行います。
        /// </summary>
        /// <remarks>
        /// <see cref="BlendShapePreset.A"/>、<see cref="BlendShapePreset.I"/>、<see cref="BlendShapePreset.O"/>が
        /// 同一のメッシュ上に存在しない、または単一のフレームを持つシェイプキーが存在しない場合、設定を行いません。
        /// 生成するシェイプキー名と同じシェイプキーが存在する場合、それを利用します。
        /// </remarks>
        /// <param name="avatar"></param>
        private static void SetLipSync(GameObject avatar)
        {
            var blendShapeProxy = avatar.GetComponent<VRMBlendShapeProxy>();
            if (!blendShapeProxy)
            {
                return;
            }

            BlendShapeAvatar blendShapeAvatar = blendShapeProxy.BlendShapeAvatar;
            if (!blendShapeAvatar)
            {
                return;
            }

            var presetsAndShapeKeyIndicesAndWeightsList = new Dictionary<BlendShapePreset, IDictionary<int, float>>();
            SkinnedMeshRenderer faceRenderer = null;
            foreach (var preset in new[] { BlendShapePreset.A, BlendShapePreset.I, BlendShapePreset.O })
            {
                var clip = blendShapeAvatar.GetClip(preset: preset);
                if (!clip)
                {
                    return;
                }

                var indecesAndWeights = clip.Values
                    .Select(binding => {
                        var invalid = new KeyValuePair<int, float>(-1, 0);

                        Transform obj = avatar.transform.Find(name: binding.RelativePath);
                        if (!obj)
                        {
                            return invalid;
                        }

                        var renderer = obj.GetComponent<SkinnedMeshRenderer>();
                        if (!renderer)
                        {
                            return invalid;
                        }
                        Mesh mesh = renderer.sharedMesh;
                        if (!mesh)
                        {
                            return invalid;
                        }

                        if (!faceRenderer)
                        {
                            faceRenderer = renderer;
                        }
                        if (renderer != faceRenderer)
                        {
                            return invalid;
                        }

                        int index = binding.Index;
                        if (index >= mesh.blendShapeCount || mesh.GetBlendShapeFrameCount(index) > 1)
                        {
                            return invalid;
                        }
                        return new KeyValuePair<int, float>(
                            index,
                            binding.Weight / VRMUtility.MaxBlendShapeBindingWeight
                        );
                    })
                    .Where(indexAndWeight => indexAndWeight.Value >= 0)
                    .ToDictionary(indexAndWeight => indexAndWeight.Key, indexAndWeight => indexAndWeight.Value);

                if (indecesAndWeights.Count() == 0)
                {
                    return;
                }

                presetsAndShapeKeyIndicesAndWeightsList[preset] = indecesAndWeights;
            }

            Mesh faceMesh = faceRenderer.sharedMesh;
            int faceMeshVertexCount = faceMesh.vertexCount;

            IDictionary<int, Vector3[]> shapeKeyIndicesAndVertices = presetsAndShapeKeyIndicesAndWeightsList
                .SelectMany(presetAndIndices => presetAndIndices.Value.Keys)
                .Distinct()
                .ToDictionary(keySelector: shapeKeyIndex => shapeKeyIndex, elementSelector: shapeKeyIndex => {
                    var deltaVertices = new Vector3[faceMeshVertexCount];
                    faceMesh.GetBlendShapeFrameVertices(shapeKeyIndex, 0, deltaVertices, null, null);
                    return deltaVertices;
                });

            foreach (var newNameAndValues in BlendShapeReplacer.VisemeShapeKeyNamesAndValues)
            {
                if (faceMesh.GetBlendShapeIndex(newNameAndValues.Key) != -1)
                {
                    continue;
                }

                Vector3[] deltaVertices = null;
                foreach (Vector3[] vertices in newNameAndValues.Value.SelectMany(presetAndWeight =>
                    presetsAndShapeKeyIndicesAndWeightsList[presetAndWeight.Key].Select(
                        shapeKeyIndexAndWeight => shapeKeyIndicesAndVertices[shapeKeyIndexAndWeight.Key]
                            .Select(vertix => vertix * shapeKeyIndexAndWeight.Value * presetAndWeight.Value)
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

                faceMesh.AddBlendShapeFrame(
                    newNameAndValues.Key,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    deltaVertices,
                    null,
                    null
                );
            }
            EditorUtility.SetDirty(target: faceMesh);

            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            avatarDescriptor.VisemeSkinnedMesh = faceRenderer;
            avatarDescriptor.VisemeBlendShapes
                = BlendShapeReplacer.VisemeShapeKeyNamesAndValues.Select(nameAndValues => nameAndValues.Key).ToArray();
        }

        /// <summary>
        /// <see cref="BlendShapePreset.Neutral"/>、および<see cref="BlendShapePreset.Blink"/>を変換します。
        /// </summary>
        /// <remarks>
        /// VRChatのプログラム側で行われる自動まばたきの無効化は行いません。
        /// 
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
        /// <returns>設定を行った場合、対象の<c>avatar</c>から対象のメッシュまでの相対パスを返します。</returns>
        private static string SetNeutralAndBlink(GameObject avatar)
        {
            AnimatorController neutralAndBlinkController
                = BlendShapeReplacer.CreateSingleAnimatorController(avatar: avatar, name: "blink");
            AnimationClip animationClip = neutralAndBlinkController.animationClips[0];

            var relativePath = "";

            foreach (var preset in new[] { BlendShapePreset.Blink, BlendShapePreset.Neutral })
            {
                BlendShapeClip clip = VRMUtility.GetBlendShapeClip(avatar: avatar, preset: preset);
                if (!clip || clip.Values == null)
                {
                    continue;
                }


                var blinkBindings = new BlendShapeBinding[] { };
                if (preset == BlendShapePreset.Neutral)
                {
                    BlendShapeClip blinkClip
                        = VRMUtility.GetBlendShapeClip(avatar: avatar, preset: BlendShapePreset.Blink);
                    if (blinkClip && blinkClip.Values != null)
                    {
                        blinkBindings = blinkClip.Values;
                    }
                }

                for (var i = 0; i < clip.Values.Length; i++)
                {
                    BlendShapeBinding binding = clip.Values[i];

                    Transform meshObject = avatar.transform.Find(name: binding.RelativePath);
                    if (!meshObject)
                    {
                        continue;
                    }

                    var renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
                    if (!renderer || !renderer.sharedMesh)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(relativePath))
                    {
                        relativePath = binding.RelativePath;
                    }
                    else if (relativePath != binding.RelativePath) {
                        continue;
                    }

                    var keys = BlendShapeReplacer.BlinkWeights;
                    if (preset == BlendShapePreset.Neutral)
                    {
                        BlendShapeBinding overlappingBinding
                            = blinkBindings.FirstOrDefault(predicate: blinkBinding => blinkBinding.Equals(binding));
                        if (!overlappingBinding.Equals(default(BlendShapeBinding)))
                        {
                            // NEUTRALとBlinkが同一のシェイプキーを参照していた場合
                            var animationCurve = new AnimationCurve();
                            foreach (var pair in BlendShapeReplacer.NeutralAndBlinkWeights)
                            {
                                float weight;
                                switch (pair.Value)
                                {
                                    case BlendShapePreset.Neutral:
                                        weight = binding.Weight;
                                        break;
                                    case BlendShapePreset.Blink:
                                        weight = overlappingBinding.Weight;
                                        break;
                                    default:
                                        weight = 0;
                                        break;
                                }
                                animationCurve.AddKey(new Keyframe(pair.Key, weight));
                            }

                            animationClip.SetCurve(
                                relativePath: "",
                                type: typeof(SkinnedMeshRenderer),
                                propertyName: "blendShape." + renderer.sharedMesh.GetBlendShapeName(binding.Index),
                                curve: animationCurve
                            );
                            continue;
                        }

                        keys = BlendShapeReplacer.NeutralWeights;
                    }

                    SetBlendShapeCurve(
                        animationClip: animationClip,
                        avatar: avatar,
                        binding: binding,
                        keys: keys,
                        setRelativePath: false
                    );
                }

                foreach (MaterialValueBinding binding in clip.MaterialValues)
                {

                    // TODO

                }
            }

            if (string.IsNullOrEmpty(relativePath)) {
                return "";
            }

            Transform neutralAndBlinkMesh = avatar.transform.Find(name: relativePath);
            neutralAndBlinkMesh.gameObject.AddComponent<Animator>().runtimeAnimatorController
                = neutralAndBlinkController;

            return relativePath;
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
            var path = Duplicator.DetermineAssetPath(
                prefabPath: AssetDatabase.GetAssetPath(PrefabUtility.GetPrefabParent(avatar)),
                type: typeof(AnimatorController),
                fileName: name
            );
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
        /// <param name="animationClip"></param>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <param name="keys"></param>
        private static void SetBlendShapeCurves(AnimationClip animationClip, GameObject avatar, BlendShapePreset preset, IDictionary<float, float> keys)
        {
            BlendShapeClip clip = VRMUtility.GetBlendShapeClip(avatar: avatar, preset: preset);
            if (!clip)
            {
                return;
            }

            foreach (BlendShapeBinding binding in clip.Values)
            {
                SetBlendShapeCurve(animationClip: animationClip, avatar: avatar, binding: binding, keys: keys);
            }

            foreach (MaterialValueBinding binding in clip.MaterialValues)
            {

                // TODO

            }
        }

        /// <summary>
        /// アニメーションクリップに、指定されたブレンドシェイプを追加します。
        /// </summary>
        /// <param name="animationClip"></param>
        /// <param name="avatar"></param>
        /// <param name="binding"></param>
        /// <param name="keys"></param>
        /// <param name="setRelativePath"></param>
        private static void SetBlendShapeCurve(
            AnimationClip animationClip,
            GameObject avatar,
            BlendShapeBinding binding,
            IDictionary<float, float> keys,
            bool setRelativePath = true
        ) {
            Transform face = avatar.transform.Find(name: binding.RelativePath);
            if (!face)
            {
                return;
            }

            var renderer = face.GetComponent<SkinnedMeshRenderer>();
            if (!renderer)
            {
                return;
            }

            var curve = new AnimationCurve();
            foreach (var pair in keys)
            {
                curve.AddKey(new Keyframe(pair.Key, pair.Value * binding.Weight));
            }

            animationClip.SetCurve(
                relativePath: setRelativePath ? binding.RelativePath : "",
                type: typeof(SkinnedMeshRenderer),
                propertyName: "blendShape." + renderer.sharedMesh.GetBlendShapeName(binding.Index),
                curve: curve
            );
        }

        /// <summary>
        /// 手の形に喜怒哀楽を割り当てます。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="relativePathToNeutralAndBlinkMesh"></param>
        private static void SetFeelings(GameObject avatar, string relativePathToNeutralAndBlinkMesh)
        {
            VRChatUtility.AddCustomAnims(avatar: avatar);

            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();

            foreach (var preset in BlendShapeReplacer.MappingBlendShapeToVRChatAnim.Keys)
            {
                if (string.IsNullOrEmpty(VRMUtility.GetFirstBlendShapeBindingName(avatar: avatar, preset: preset)))
                {
                    continue;
                }

                AnimationClip clip = CreateFeeling(
                    avatar: avatar,
                    preset: preset,
                    relativePathToNeutralAndBlinkMesh: relativePathToNeutralAndBlinkMesh
                );
                string anim = BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset].ToString();
                avatarDescriptor.CustomStandingAnims[anim] = clip;
                avatarDescriptor.CustomSittingAnims[anim] = clip;
            }
        }

        /// <summary>
        /// 表情の設定を行うアニメーションクリップを作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <param name="relativePathToNeutralAndBlinkMesh"></param>
        /// <returns></returns>
        private static AnimationClip CreateFeeling(
            GameObject avatar,
            BlendShapePreset preset,
            string relativePathToNeutralAndBlinkMesh
        ) {
            var anim = Duplicator.DuplicateAssetToFolder<AnimationClip>(
                source: UnityPath.FromUnityPath(Converter.RootFolderPath).Child("Editor")
                    .Child(BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset] + ".anim").LoadAsset<AnimationClip>(),
                prefabInstance: avatar,
                fileName: preset + ".anim"
            );

            if (!string.IsNullOrEmpty(relativePathToNeutralAndBlinkMesh))
            {
                var curve = new AnimationCurve();
                foreach (var timeAndValue in BlendShapeReplacer.NeutralAndBlinkStopperWeights)
                {
                    curve.AddKey(new Keyframe(timeAndValue.Key, timeAndValue.Value));
                }
                anim.SetCurve(
                    relativePath: relativePathToNeutralAndBlinkMesh,
                    type: typeof(Behaviour),
                    propertyName: "m_Enabled",
                    curve: curve
                );

                BlendShapeReplacer.DuplicateShapeKeyToUnique(
                    avatar: avatar,
                    preset: preset,
                    relativePathToNeutralAndBlinkMesh: relativePathToNeutralAndBlinkMesh
                );
            }

            SetBlendShapeCurves(animationClip: anim, avatar: avatar, preset: preset, keys: new Dictionary<float, float> {
                { 0, 1 },
                { anim.length, 1 },
            });

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
        /// <param name="preset"></param>
        /// <param name="relativePathToNeutralAndBlinkMesh"></param>
        private static void DuplicateShapeKeyToUnique(
            GameObject avatar,
            BlendShapePreset preset,
            string relativePathToNeutralAndBlinkMesh
        )
        {
            BlendShapeAvatar blendShapeAvatar = avatar.GetComponent<VRMBlendShapeProxy>().BlendShapeAvatar;

            int[] neutralAndBlinkShapeKeyIndices = new[] { BlendShapePreset.Neutral, BlendShapePreset.Blink }
                .Select(selector: blendShapePreset => blendShapeAvatar.GetClip(preset: blendShapePreset))
                .SelectMany(selector: blendShapeClip => blendShapeClip.Values)
                .Where(predicate: binding => binding.RelativePath == relativePathToNeutralAndBlinkMesh)
                .Select(selector: binding => binding.Index)
                .ToArray();

            BlendShapeClip clip = blendShapeAvatar.GetClip(preset: preset);
            Mesh mesh = avatar.transform.Find(name: relativePathToNeutralAndBlinkMesh)
                .GetComponent<SkinnedMeshRenderer>().sharedMesh;
            for (var i = 0; i < clip.Values.Length; i++)
            {
                BlendShapeBinding binding = clip.Values[i];
                if (binding.RelativePath != relativePathToNeutralAndBlinkMesh
                    || !neutralAndBlinkShapeKeyIndices.Contains(binding.Index))
                {
                    continue;
                }

                var name = BlendShapeReplacer.FeelingsShapeKeyPrefix + mesh.GetBlendShapeName(binding.Index);
                var frameCount = mesh.GetBlendShapeFrameCount(shapeIndex: binding.Index);
                for (var j = 0; j < frameCount; j++)
                {
                    var deltaVertices = new Vector3[mesh.vertexCount];
                    var deltaNormals = new Vector3[mesh.vertexCount];
                    var deltaTangents = new Vector3[mesh.vertexCount];

                    mesh.GetBlendShapeFrameVertices(
                        shapeIndex: binding.Index,
                        frameIndex: j,
                        deltaVertices: deltaVertices,
                        deltaNormals: deltaNormals,
                        deltaTangents: deltaTangents
                    );

                    mesh.AddBlendShapeFrame(
                        shapeName: name,
                        frameWeight: mesh.GetBlendShapeFrameWeight(shapeIndex: binding.Index, frameIndex: j),
                        deltaVertices: deltaVertices,
                        deltaNormals: deltaNormals,
                        deltaTangents: deltaTangents
                    );
                }

                EditorUtility.SetDirty(target: mesh);

                binding.Index = mesh.GetBlendShapeIndex(blendShapeName: name);
                VRMUtility.ReplaceBlendShapeBinding(
                    blendShapeAvatar: blendShapeAvatar,
                    oldBinding: clip.Values[i],
                    newBinding: binding
                );
            }
        }
    }
}
