#nullable enable
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
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

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
        /// まばたき用に生成するシェイプキー名。
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
        private static readonly Dictionary<BlendShapePreset, VRChatUtility.Anim> MappingBlendShapeToVRChatAnim = new()
        {
            { BlendShapePreset.Joy, VRChatUtility.Anim.VICTORY },
            { BlendShapePreset.Angry, VRChatUtility.Anim.HANDGUN },
            { BlendShapePreset.Sorrow, VRChatUtility.Anim.THUMBSUP },
            { BlendShapePreset.Fun, VRChatUtility.Anim.ROCKNROLL },
            { BlendShapePreset.Unknown, VRChatUtility.Anim.FINGERPOINT },
        };

        /// <summary>
        /// 各表情の上から時計回りの位置。
        /// </summary>
        private static readonly IList<BlendShapePreset> FacialExpressionsOrder = new[]
        {
            BlendShapePreset.Sorrow,
            BlendShapePreset.Joy,
            BlendShapePreset.Fun,
            BlendShapePreset.Angry,
        }.ToList();

        /// <summary>
        /// 表情用のテンプレートAnimatorControllerのGUID。
        /// </summary>
        private static readonly string FXTemplateGUID = "4dab2bc02bfaabc4faea4c6b4d8a142b";

        /// <summary>
        /// 表情用の<see cref="VRCExpressionsMenu"/>のGUID。
        /// </summary>
        private static readonly string VRCExpressionsMenuGUID = "91a5a0002bd103f448d572330b087f57";

        /// <summary>
        /// デフォルトの<see cref="VRCExpressionsMenu"/>を再現したアセットのGUID。
        /// </summary>
        private static readonly string VRCEmoteGUID = "b824836cefba43040b1cfce3a0859812";

        /// <summary>
        /// 表情用の<see cref="VRCExpressionParameters"/>のGUID。
        /// </summary>
        private static readonly string VRCExpressionParametersGUID = "d492b41c65685944a96df77628e204bc";

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
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useShapeKeyNormalsAndTangents"></param>
        /// <param name="vrmBlendShapeForFINGERPOINT"></param>
        /// <param name="oscComponents"></param>
        internal static void Apply(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents,
            VRMBlendShapeClip? vrmBlendShapeForFINGERPOINT,
            Converter.OSCComponents oscComponents
        )
        {
            SetLipSync(avatar, clips, useShapeKeyNormalsAndTangents);

            EnableEyeLook(avatar, clips, useShapeKeyNormalsAndTangents, oscComponents);

            SetFeelings(avatar, clips, vrmBlendShapeForFINGERPOINT, oscComponents);
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

                mesh.AddBlendShapeFrame(
                    newName,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.SumVerticesList(values
                        .SelectMany(presetAndWeight =>
                            BlendShapeReplacer.SubtractNeutralShapeKeyValues(clips.First(clip => clip.Preset == presetAndWeight.Key).ShapeKeyValues, clips)
                                .Select(shapeKeyNameAndWeight => shapeKeys
                                    .First(shapeKey => shapeKey.Name == shapeKeyNameAndWeight.Key)
                                    .Positions
                                    .Select(vertix => vertix * (shapeKeyNameAndWeight.Value / VRMUtility.MaxBlendShapeBindingWeight * presetAndWeight.Value))
                                )
                        )).ToArray(),
                    null,
                    null
                );
            }
            EditorUtility.SetDirty(mesh);

            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            avatarDescriptor.VisemeSkinnedMesh = renderer;
            avatarDescriptor.VisemeBlendShapes
                = BlendShapeReplacer.VisemeShapeKeyNamesAndValues.Select(nameAndValues => nameAndValues.Key).ToArray();
        }

        /// <summary>
        /// 指定されたシェイプキーを合成し、新しいシェイプキーを作成します。
        /// </summary>
        /// <param name="namesAndWeights">シェイプキー名と0〜100のウェイトの連想配列。</param>
        /// <param name="shapeKeys"><see cref="SkinnedMeshUtility.GetAllShapeKeys"/>の戻り値。</param>
        /// <returns></returns>
        private static IEnumerable<Vector3> GenerateShapeKey(
            IDictionary<string, float> namesAndWeights,
            IEnumerable<BlendShape> shapeKeys

        )
        {
            return BlendShapeReplacer.SumVerticesList(namesAndWeights
                .Select(nameAndWeight => shapeKeys.First(shapeKey => shapeKey.Name == nameAndWeight.Key)
                .Positions
                .Select(vertix => vertix * (nameAndWeight.Value / VRMUtility.MaxBlendShapeBindingWeight)))
            );
        }

        /// <summary>
        /// 要素数が同じな複数のVector3配列で、同じインデックス同士を加算して返します。
        /// </summary>
        /// <param name="verticesList"></param>
        /// <returns></returns>
        private static IEnumerable<Vector3> SumVerticesList(IEnumerable<IEnumerable<Vector3>> verticesList)
        {
            return verticesList.SelectMany(vertices => vertices.Select((vertix, index) => (vertix, index)))
                .GroupBy(vertixAndIndex => vertixAndIndex.index, vertixAndIndex => vertixAndIndex.vertix)
                .Select(vertices => vertices.ToList().Aggregate((accumulate, source) => accumulate + source));
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
        /// <see cref="BlendShapePreset.Blink"/>を変換し、視線追従を有効化します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="useShapeKeyNormalsAndTangents"/>
        /// <param name="oscComponents"/>
        private static void EnableEyeLook(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            bool useShapeKeyNormalsAndTangents,
            Converter.OSCComponents oscComponents
        )
        {
            var oscBlinkEnabled = oscComponents.HasFlag(Converter.OSCComponents.Blink);
            var lookAtBoneApplyer = avatar.GetComponent<VRMLookAtBoneApplyer>();
            if (oscBlinkEnabled && !lookAtBoneApplyer)
            {
                return;
            }

            var renderer = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath).GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer.sharedMesh;
            if (!oscBlinkEnabled && mesh.GetBlendShapeIndex(BlendShapeReplacer.BlinkShapeKeyName) == -1)
            {
                mesh.AddBlendShapeFrame(
                    BlendShapeReplacer.BlinkShapeKeyName,
                    BlendShapeReplacer.MaxBlendShapeFrameWeight,
                    BlendShapeReplacer.GenerateShapeKey(
                        BlendShapeReplacer.SubtractNeutralShapeKeyValues(
                            clips.FirstOrDefault(c => c.Preset == BlendShapePreset.Blink).ShapeKeyValues,
                            clips
                        ),
                        SkinnedMeshUtility.GetAllShapeKeys(mesh, useShapeKeyNormalsAndTangents)
                    ).ToArray(),
                    null,
                    null
                );
                EditorUtility.SetDirty(mesh);
            }

            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            descriptor.enableEyeLook = true;

            var settings = new VRCAvatarDescriptor.CustomEyeLookSettings();

            if (!oscBlinkEnabled)
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
        /// 手の形に喜怒哀楽を割り当てます。また、OSCの設定を行います。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="clips"></param>
        /// <param name="vrmBlendShapeForFINGERPOINT"></param>
        /// <param name="oscComponents"></param>
        private static void SetFeelings(
            GameObject avatar,
            IEnumerable<VRMBlendShapeClip> clips,
            VRMBlendShapeClip? vrmBlendShapeForFINGERPOINT,
            Converter.OSCComponents oscComponents
        )
        {
            var usedPresets = new List<BlendShapePreset>();
            var animationClips = new List<AnimationClip>();

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

            AnimationClip? neutral = null;

            foreach (var preset in BlendShapeReplacer.MappingBlendShapeToVRChatAnim.Keys.Concat(new[] { BlendShapePreset.Neutral }))
            {
                VRMBlendShapeClip? blendShapeClip = preset == BlendShapePreset.Unknown
                    ? vrmBlendShapeForFINGERPOINT
                    : clips.FirstOrDefault(c => c.Preset == preset);
                if (blendShapeClip == null)
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

                var animationClip = CreateFeeling(avatar, blendShapeClip);
                if (preset == BlendShapePreset.Neutral)
                {
                    neutral = animationClip;
                }
                else
                {
                    childStates
                        .First(childState => childState.state.name.ToLower() == BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset].ToString().ToLower())
                        .state.motion = animationClip;
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
            }
            motions[0].motion = neutral;
            blendTree.children = motions;

            // Write Defaultsが無効でも動作するように、各アニメーションクリップへ初期値を追加
            var materials = avatar.transform.Find(VRChatUtility.AutoBlinkMeshPath)
                .GetComponent<SkinnedMeshRenderer>().sharedMaterials;

            foreach (var clip in usedPresets.Select(preset => clips.First(clip => preset == BlendShapePreset.Unknown
                ? (vrmBlendShapeForFINGERPOINT != null && clip.BlendShapeName == vrmBlendShapeForFINGERPOINT.BlendShapeName)
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

            // OSC設定
            if (oscComponents.HasFlag(Converter.OSCComponents.Blink))
            {
                foreach (var preset in new[] { BlendShapePreset.Blink_L, BlendShapePreset.Blink_R })
                {
                    fxController.layers.First(layer =>
                        layer.name == (preset == BlendShapePreset.Blink_L ? "OSC Blink Left" : "OSC Blink Right"))
                            .stateMachine.states.First(childState => childState.state.name == "Blink").state.motion
                                = BlendShapeReplacer.CreateFeeling(
                                    avatar,
                                    clips.FirstOrDefault(clip => clip.Preset == preset),
                                    forMotionTime: true
                                );
                }
                fxController.layers = fxController.layers.Where(layer => layer.name != "Stop Blink").ToArray();
            }
            else
            {
                fxController.layers = fxController.layers.Where(layer => !layer.name.StartsWith("OSC Blink")).ToArray();
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
        }

        /// <summary>
        /// 表情の設定を行うアニメーションクリップを作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="vrmBlendShape"></param>
        /// <param name="clips"></param>
        /// <param name="forMotionTime"></param>
        /// <returns></returns>
        private static AnimationClip CreateFeeling(
            GameObject avatar,
            VRMBlendShapeClip clip,
            bool forMotionTime = false
        )
        {
            var anim = Duplicator.CreateObjectToFolder(
                source: new AnimationClip(),
                prefabInstance: avatar,
                destinationFileName: clip.Preset + ".anim"
            );

            AssetDatabase.SaveAssets(); // 新規作成された.animのプロパティが保存されない不具合を回避

            SetBlendShapeCurves(
                avatar,
                animationClip: anim,
                clip: clip,
                keys: new Dictionary<float, float> {
                    { 0, forMotionTime ? 0 : 1 },
                    { anim.length, 1 },
                }
            );

            return anim;
        }
    }
}
