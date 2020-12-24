using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using Esperecyan.Unity.VRMConverterForVRChat.Utilities;
using Esperecyan.Unity.VRMConverterForVRChat.Components;

namespace Esperecyan.Unity.VRMConverterForVRChat.VRChatToVRM
{
    /// <summary>
    /// Expressionに関する設定。
    /// </summary>
    internal class VRChatExpressionsReplacer
    {
        /// <summary>
        /// アニメーションクリップでキーがシェイプキーの場合、値がこれ以下なら存在しないものとみなします。
        /// </summary>
        private static readonly float ShapeKeyAnimationThreshold = 1;

        /// <summary>
        /// シェイプキー名とウェイト (0〜100) を取得します。
        /// </summary>
        /// <param name="vrchatExpressionBinding"></param>
        /// <returns></returns>
        internal static IDictionary<string, float> ExtractShapeKeyNames(VRChatExpressionBinding vrchatExpressionBinding)
        {
            var animationClip = vrchatExpressionBinding.AnimationClip;
            return animationClip != null
                ? AnimationUtility.GetCurveBindings(animationClip)
                    .Where(editorCurveBinding =>
                    {
                        if (editorCurveBinding.type != typeof(SkinnedMeshRenderer)
                            || !editorCurveBinding.propertyName.StartsWith("blendShape."))
                        {
                            if (editorCurveBinding.type != typeof(Animator))
                            {
                                Debug.Log($"アニメーション「{animationClip.name}」のキー「{editorCurveBinding.path}.{editorCurveBinding.type}.{editorCurveBinding.propertyName}」はシェイプキーでないため変換できません。");
                            }
                            return false;
                        }

                        return true;
                    })
                    .Select(editorCurveBinding => (
                        shapeKeyName: Regex.Replace(editorCurveBinding.propertyName, @"^blendShape\.", ""),
                        weight:  AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding).keys[0].value
                    ))
                    .Where(shapeKeyNameWeightPair =>
                        shapeKeyNameWeightPair.weight > VRChatExpressionsReplacer.ShapeKeyAnimationThreshold)
                    .ToDictionary(
                        shapeKeyNameWeightPair => shapeKeyNameWeightPair.shapeKeyName,
                        shapeKeyNameWeightPair => shapeKeyNameWeightPair.weight
                    )
                : vrchatExpressionBinding.ShapeKeyNames.ToDictionary(
                    shapeKeyName => shapeKeyName,
                    _ => BlendShapeReplacer.MaxBlendShapeFrameWeight
                );
        }

        /// <summary>
        /// Expressionの設定を行います。
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="presetShapeKeyNameWeightPairsPairs"></param>
        internal static void SetExpressions(
            GameObject instance,
            IDictionary<ExpressionPreset, IDictionary<string, float>> presetShapeKeyNameWeightPairsPairs
        )
        {
            var relativePathShapeKeyNamesPairs
                = VRChatExpressionsReplacer.GetRelativePathShapeKeyNamesPairs(instance);
            var blendShapeAvatar = instance.GetComponent<VRMBlendShapeProxy>().BlendShapeAvatar;

            foreach (var (preset, shapeKeyNameWeightPairs) in presetShapeKeyNameWeightPairsPairs)
            {
                var values = shapeKeyNameWeightPairs
                    .Select(shapeKeyNameWeightPair => VRChatExpressionsReplacer.ConvertBinding(
                        relativePathShapeKeyNamesPairs,
                        shapeKeyNameWeightPair.Key,
                        shapeKeyNameWeightPair.Value
                    ))
                    .Where(value => value != null)
                    .Select(value => value.Value);

                VRChatExpressionsReplacer.GetExpression(blendShapeAvatar, preset).Values = values.ToArray();
            }
        }

        /// <summary>
        /// 子孫のメッシュの<see cref="BlendShapeBinding.RelativePath"/>とシェイプキー名のリストの組を取得します。
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        private static IDictionary<string, IList<string>> GetRelativePathShapeKeyNamesPairs(GameObject instance)
        {
            return instance.GetComponentsInChildren<SkinnedMeshRenderer>()
                .ToDictionary(renderer => renderer.transform.RelativePathFrom(instance.transform), renderer =>
                {
                    var mesh = renderer.sharedMesh;
                    var names = new List<string>();
                    for (var i = 0; i < mesh.blendShapeCount; i++)
                    {
                        names.Add(mesh.GetBlendShapeName(i));
                    }
                    return (IList<string>)names;
                });
        }

        /// <summary>
        /// <see cref="VRChatExpressionsReplacer.GetRelativePathShapeKeyNamesPairs"/>の戻り値から指定されたシェイプキーを検索して返します。
        /// </summary>
        /// <param name="relativePathShapeKeyNamesPairs"></param>
        /// <param name="shapeKeyName"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        private static BlendShapeBinding? ConvertBinding(
            IDictionary<string, IList<string>> relativePathShapeKeyNamesPairs,
            string shapeKeyName,
            float weight
        )
        {
            foreach (var (relativePath, shapeKeyNames) in relativePathShapeKeyNamesPairs)
            {
                var index = shapeKeyNames.IndexOf(shapeKeyName);
                if (index != -1)
                {
                    return new BlendShapeBinding()
                    {
                        RelativePath = relativePath,
                        Index = index,
                        Weight = weight,
                    };
                }
            }
            return null;
        }

        private static BlendShapeClip GetExpression(BlendShapeAvatar blendShapeAvatar, ExpressionPreset preset)
        {
            switch (preset)
            {
                case ExpressionPreset.Aa:
                    return blendShapeAvatar.GetClip(BlendShapePreset.A);
                case ExpressionPreset.Ih:
                    return blendShapeAvatar.GetClip(BlendShapePreset.I);
                case ExpressionPreset.Ou:
                    return blendShapeAvatar.GetClip(BlendShapePreset.U);
                case ExpressionPreset.Ee:
                    return blendShapeAvatar.GetClip(BlendShapePreset.E);
                case ExpressionPreset.Oh:
                    return blendShapeAvatar.GetClip(BlendShapePreset.O);
                case ExpressionPreset.Happy:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Joy);
                case ExpressionPreset.Angry:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Angry);
                case ExpressionPreset.Sad:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Sorrow);
                case ExpressionPreset.Relaxed:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Fun);
                case ExpressionPreset.Surprised:
                    var blendShapeClip = ScriptableObject.CreateInstance<BlendShapeClip>();
                    blendShapeClip.BlendShapeName = "Surprised";
                    blendShapeAvatar.Clips.Add(blendShapeClip);
                    return blendShapeClip;
                case ExpressionPreset.Blink:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Blink);
                case ExpressionPreset.BlinkLeft:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Blink_L);
                case ExpressionPreset.BlinkRight:
                    return blendShapeAvatar.GetClip(BlendShapePreset.Blink_R);
            }
            throw new ArgumentOutOfRangeException();
        }
    }
}
