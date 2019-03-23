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

        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.VisemeBlendShapes"/>に対応する、Cats Blender PluginでVRChat用に生成されるシェイプキー名。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// cats-blender-plugin/viseme.py at master · michaeldegroot/cats-blender-plugin
        /// <https://github.com/michaeldegroot/cats-blender-plugin/blob/master/tools/viseme.py>
        /// </remarks>
        private static readonly string[] OrderedVisemusGeneratedByCatsBlenderPlugin = {
            "vrc.v_sil",
            "vrc.v_pp",
            "vrc.v_ff",
            "vrc.v_th",
            "vrc.v_dd",
            "vrc.v_kk",
            "vrc.v_ch",
            "vrc.v_ss",
            "vrc.v_nn",
            "vrc.v_rr",
            "vrc.v_aa",
            "vrc.v_e",
            "vrc.v_ih",
            "vrc.v_oh",
            "vrc.v_ou",
        };

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
        /// まばたき干渉防止用のAnimatorコンポーネントを設定するオブジェクト名。
        /// </summary>
        private static readonly string BlinkStopperObjectName = "vrc.blink-stopper";

        /// <summary>
        /// まばたき干渉防止用のアニメーションクリップの設定値。キーに秒、値に有効無効。
        /// </summary>
        private static readonly Dictionary<float, float> BlinkStopperWeights = new Dictionary<float, float> {
            {  0.00f, 0 },
            {  0.10f, 0 },
            {  0.11f, 1 },
            {  3.00f, 1 },
        };

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

            SetNeutral(avatar: avatar);

            var relativePathToBlinkMesh = SetAutoBlink(avatar: avatar);

            SetFeelings(avatar: avatar, relativePathToBlinkMesh: relativePathToBlinkMesh);
        }

        /// <summary>
        /// リップシンクの設定を行います。
        /// </summary>
        /// <remarks>
        /// <see cref="BlendShapeReplacer.OrderedVisemusGeneratedByCatsBlenderPlugin"/>が存在する場合、VRMの設定を無視します。
        /// </remarks>
        /// <param name="avatar"></param>
        private static void SetLipSync(GameObject avatar)
        {
            var renderer = VRMUtility.GetFirstSkinnedMeshRenderer(avatar: avatar, preset: BlendShapePreset.A);
            if (!renderer)
            {
                return;
            }
            var mesh = renderer.sharedMesh;

            if (BlendShapeReplacer.OrderedVisemusGeneratedByCatsBlenderPlugin.Except(GetBlendShapeNames(mesh)).Count() == 0)
            {
                var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
                avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                avatarDescriptor.VisemeSkinnedMesh = renderer;
                avatarDescriptor.VisemeBlendShapes = BlendShapeReplacer.OrderedVisemusGeneratedByCatsBlenderPlugin;
            }
            else
            {
                var names = new Dictionary<BlendShapePreset, string> {
                    { BlendShapePreset.A, "" },
                    { BlendShapePreset.I, "" },
                    { BlendShapePreset.U, "" },
                    { BlendShapePreset.E, "" },
                    { BlendShapePreset.O, "" },
                };
                renderer = null;
                foreach (var preset in names.Keys.ToArray())
                {
                    var r = VRMUtility.GetFirstSkinnedMeshRenderer(avatar: avatar, preset: preset);
                    if (!r || renderer && r != renderer)
                    {
                        return;
                    }
                    renderer = r;
                    names[preset] = VRMUtility.GetFirstBlendShapeBindingName(avatar: avatar, preset: preset);
                }


                var neutralRenderer = VRMUtility.GetFirstSkinnedMeshRenderer(avatar: avatar, preset: BlendShapePreset.Neutral);

                var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
                avatarDescriptor.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                avatarDescriptor.VisemeSkinnedMesh = renderer;
                avatarDescriptor.VisemeBlendShapes = new string[]{
                    neutralRenderer && neutralRenderer == renderer
                        ? VRMUtility.GetFirstBlendShapeBindingName(avatar: avatar, preset: BlendShapePreset.Neutral)
                        : names[BlendShapePreset.E],
                    names[BlendShapePreset.E],
                    names[BlendShapePreset.E],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.I],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.E],
                    names[BlendShapePreset.A],
                    names[BlendShapePreset.O],
                    names[BlendShapePreset.U],
                };
            }
        }

        /// <summary>
        /// 自動まばたきの設定を行います。
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
        /// </remarks>
        /// <param name="avatar"></param>
        /// <returns>自動まばたきの設定を行った場合、対象の<c>avatar</c>から対象のメッシュまでの相対パスを返します。</returns>
        private static string SetAutoBlink(GameObject avatar)
        {
            if (string.IsNullOrEmpty(VRMUtility.GetFirstBlendShapeBindingName(avatar: avatar, preset: BlendShapePreset.Blink)))
            {
                return "";
            }

            var controllerTemplate
                = UnityPath.FromUnityPath(Converter.RootFolderPath).Child("Editor").Child("blink.controller").LoadAsset<AnimatorController>();

            var blinkController = Duplicator.DuplicateAssetToFolder<AnimatorController>(
                source: controllerTemplate,
                prefabInstance: avatar
            );
            var animationClip = UnityPath.FromAsset(blinkController).LoadAsset<AnimationClip>();

            BlendShapeClip clip = VRMUtility.GetBlendShapeClip(avatar: avatar, preset: BlendShapePreset.Blink);

            var relativePath = "";
            for (var i = 0; i < clip.Values.Length; i++)
            {
                BlendShapeBinding binding = clip.Values[i];
                
                var blendShapeName = "blink-" + Random.value;
                if (!DuplicateBlendShape(avatar: avatar, binding: binding, newBlendShapeName: blendShapeName))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(relativePath))
                {
                    relativePath = binding.RelativePath;

                    if (!relativePath.Contains("/"))
                    {
                        // まばたきが入ったメッシュがルート直下に存在すれば
                        Transform dummyObject = new GameObject(name: BlendShapeReplacer.BlinkStopperObjectName).transform;
                        dummyObject.parent = avatar.transform;
                        avatar.transform.Find(relativePath).parent = dummyObject.transform;
                        VRMUtility.ReplaceBlendShapeRelativePaths(
                            avatar: avatar,
                            oldPath: relativePath,
                            newPath: dummyObject.name + "/" + relativePath
                        );
                        binding = clip.Values[i];
                        relativePath = dummyObject.name + "/" + relativePath;
                    }
                }
                else if (relativePath != binding.RelativePath) {
                    continue;
                }

                SetBlendShapeCurve(
                    animationClip: animationClip,
                    avatar: avatar,
                    binding: binding,
                    keys: BlendShapeReplacer.BlinkWeights,
                    setRelativePath: false
                );
            }

            foreach (MaterialValueBinding binding in clip.MaterialValues)
            {

                // TODO

            }

            if (string.IsNullOrEmpty(relativePath)) {
                return "";
            }

            Transform blinkMesh = avatar.transform.Find(name: relativePath);
            blinkMesh.gameObject.AddComponent<Animator>().runtimeAnimatorController = blinkController;

            // 親にまばたき干渉回避用のコントローラーを入れる
            Transform parent = blinkMesh.parent;
            var blinkStopperController = Duplicator.DuplicateAssetToFolder<AnimatorController>(
                source: controllerTemplate,
                prefabInstance: avatar,
                fileName: "blink-stopper.controller"
            );
            animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath: AssetDatabase.GetAssetPath(blinkStopperController));
            var curve = new AnimationCurve();
            foreach (var pair in BlendShapeReplacer.BlinkStopperWeights)
            {
                curve.AddKey(new Keyframe(pair.Key, pair.Value));
            }
            animationClip.SetCurve(
                relativePath: blinkMesh.name,
                type: typeof(Behaviour),
                propertyName: "m_Enabled",
                curve: curve
            );
            var animator = parent.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = blinkStopperController;
            animator.enabled = false;

            return relativePath;
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
                curve.AddKey(time: pair.Key, value: pair.Value * binding.Weight);
            }

            animationClip.SetCurve(
                relativePath: setRelativePath ? binding.RelativePath : "",
                type: typeof(SkinnedMeshRenderer),
                propertyName: "blendShape." + renderer.sharedMesh.GetBlendShapeName(binding.Index),
                curve: curve
            );
        }

        /// <summary>
        /// <see cref="VRC_AvatarDescriptor.CustomStandingAnims"/>を取得、または作成して返します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        private static AnimatorOverrideController GetOrAddCustomStandingAnims(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();

            if (!avatarDescriptor.CustomStandingAnims)
            {
                avatar.GetOrAddComponent<VRC_AvatarDescriptor>().CustomStandingAnims = Duplicator.DuplicateAssetToFolder<AnimatorOverrideController>(
                    source: AssetDatabase.LoadMainAssetAtPath(VRChatUtility.CustomStandingAnimsPath),
                    prefabInstance: avatar,
                    fileName: "CustomStandingAnims.overrideController"
                );
            }

            return avatarDescriptor.CustomStandingAnims;
        }

        /// <summary>
        /// 手の形に喜怒哀楽を割り当てます。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="relativePathToBlinkMesh">
        private static void SetFeelings(GameObject avatar, string relativePathToBlinkMesh)
        {
            AnimatorOverrideController customStandingAnims = GetOrAddCustomStandingAnims(avatar: avatar);

            foreach (var preset in BlendShapeReplacer.MappingBlendShapeToVRChatAnim.Keys)
            {
                if (string.IsNullOrEmpty(VRMUtility.GetFirstBlendShapeBindingName(avatar: avatar, preset: preset)))
                {
                    continue;
                }

                customStandingAnims[BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset].ToString()]
                    = CreateFeeling(avatar: avatar, preset: preset, relativePathToBlinkMesh: relativePathToBlinkMesh);
            }
        }

        /// <summary>
        /// 標準の表情を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void SetNeutral(GameObject avatar)
        {
            avatar.GetComponent<VRMBlendShapeProxy>().SetValues(values: new Dictionary<BlendShapeKey, float> {
                { new BlendShapeKey(preset: BlendShapePreset.Neutral), 1.0f },
            });
        }

        /// <summary>
        /// 表情の設定を行うアニメーションクリップを作成します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="preset"></param>
        /// <param name="relativePathToBlinkMesh"><c>avatar</c>からまばたきが設定されたメッシュまでの相対パス。</param>
        /// <returns></returns>
        private static AnimationClip CreateFeeling(GameObject avatar, BlendShapePreset preset, string relativePathToBlinkMesh)
        {
            var anim = Duplicator.DuplicateAssetToFolder<AnimationClip>(
                source: UnityPath.FromUnityPath(Converter.RootFolderPath).Child("Editor")
                    .Child(BlendShapeReplacer.MappingBlendShapeToVRChatAnim[preset] + ".anim").LoadAsset<AnimationClip>(),
                prefabInstance: avatar,
                fileName: preset + ".anim"
            );

            SetBlendShapeCurves(animationClip: anim, avatar: avatar, preset: preset, keys: new Dictionary<float, float> {
                { 0, 1 },
                { anim.length, 1 },
            });

            if (!string.IsNullOrEmpty(relativePathToBlinkMesh))
            {
                var curve = new AnimationCurve();
                curve.AddKey(time: 0, value: 1);
                curve.AddKey(time: anim.length, value: 1);
                string[] path = relativePathToBlinkMesh.Split(separator: '/');
                anim.SetCurve(
                    relativePath: string.Join(separator: "/", value: path, startIndex: 0, count: path.Length - 1),
                    type: typeof(Behaviour),
                    propertyName: "m_Enabled",
                    curve: curve
                );
            }

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
        /// 指定されたブレンドシェイプを複製します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="binding"></param>
        /// <param name="newBlendShapeName"></param>
        /// <returns>対応するメッシュが見つからなかった場合、<c>false</c> を返します。</returns>
        private static bool DuplicateBlendShape(GameObject avatar, BlendShapeBinding binding, string newBlendShapeName)
        {
            Transform face = avatar.transform.Find(name: binding.RelativePath);
            if (!face)
            {
                return false;
            }

            var renderer = face.GetComponent<SkinnedMeshRenderer>();
            if (!renderer)
            {
                return false;
            }

            Mesh mesh = renderer.sharedMesh;

            var frameCount = mesh.GetBlendShapeFrameCount(shapeIndex: binding.Index);
            for (var i = 0; i < frameCount; i++)
            {
                var deltaVertices = new Vector3[mesh.vertexCount];
                var deltaNormals = new Vector3[mesh.vertexCount];
                var deltaTangents = new Vector3[mesh.vertexCount];

                mesh.GetBlendShapeFrameVertices(
                    shapeIndex: binding.Index,
                    frameIndex: i,
                    deltaVertices: deltaVertices,
                    deltaNormals: deltaNormals,
                    deltaTangents: deltaTangents
                );

                mesh.AddBlendShapeFrame(
                    shapeName: newBlendShapeName,
                    frameWeight: mesh.GetBlendShapeFrameWeight(shapeIndex: binding.Index, frameIndex: i),
                    deltaVertices: deltaVertices,
                    deltaNormals: deltaNormals,
                    deltaTangents: deltaTangents
                );
            }

            EditorUtility.SetDirty(target: mesh);

            binding.Index = mesh.GetBlendShapeIndex(blendShapeName: newBlendShapeName);

            return true;
        }
    }
}
