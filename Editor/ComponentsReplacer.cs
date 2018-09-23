using System;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using VRM;
using VRCSDK2;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// キャラクター情報、視点、揺れ物に関する設定。
    /// </summary>
    [InitializeOnLoad]
    public class ComponentsReplacer
    {
        /// <summary>
        /// 揺れ物のパラメータ変換アルゴリズムの定義を行うコールバック関数。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public delegate DynamicBoneParameters SwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo);

        /// <summary>
        /// バーチャルキャストにおいて、該当ボーンの<see cref="VRMSpringBoneColliderGroup">が、他アバターの<see cref="VRMSpringBone.ColliderGroups">に設定される。
        /// </summary>
        public static HumanBodyBones[] BonesForCollisionWithOtherAvatarOnVirtualCast = {
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
        };

        static ComponentsReplacer()
        {
            SuppressCompilerError();
        }

        /// <summary>
        /// <see cref="ComponentsReplacer.SwayingParametersConverter">の既定値。
        /// </summary>
        /// <param name="springBoneParameters"></param>
        /// <param name="boneInfo"></param>
        /// <returns></returns>
        public static DynamicBoneParameters DefaultSwayingParametersConverter(SpringBoneParameters springBoneParameters, BoneInfo boneInfo)
        {
            return new DynamicBoneParameters()
            {
                Elasticity = springBoneParameters.StiffnessForce * 0.05f,
                Damping = springBoneParameters.DragForce * 0.6f,
                Stiffness = 0,
                Inert = 0,
            };
        }

        /// <summary>
        /// クラスに含まれる処理を適用します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="defaultAnimationSet"></param>
        /// <param name="swayingParametersConverter"></param>
        internal static void Apply(
            GameObject avatar,
            VRC_AvatarDescriptor.AnimationSet defaultAnimationSet,
            ComponentsReplacer.SwayingParametersConverter swayingParametersConverter
        )
        {
            ConvertMeta(avatar: avatar, defaultAnimationSet: defaultAnimationSet);
            ConvertVRMFirstPerson(avatar: avatar);
            SetCollidersForCollisionWithOtherAvatar(avatar: avatar);

            var swayingObjectsConverter = Type.GetType("SwayingObjectsConverter, Assembly-CSharp-Editor");
            if (swayingObjectsConverter != null) {
                swayingObjectsConverter.InvokeMember(
                    name: "Apply",
                    invokeAttr: BindingFlags.InvokeMethod,
                    binder: null,
                    target: null,
                    args: new object[] { avatar, swayingParametersConverter }
                );
            }
        }

        /// <summary>
        /// Dynamic Boneアセットがインポートされていない場合、同アセットに含まれるクラスを利用しているスクリプトファイルを無効化します。
        /// </summary>
        private static void SuppressCompilerError()
        {
            if (Type.GetType("DynamicBone, Assembly-CSharp") == null
                && AssetDatabase.FindAssets(filter: "SwayingObjectsConverter.cs", searchInFolders: new[] { CurrentFolderGetter.Get() }).Length == 0) {
                // Dynamicボーンが存在しない、
                // かつ拡張子を除いたファイル名が「SwayingObjectsConverter.cs」になるファイルが存在しなければ
                var path = Path.Combine(CurrentFolderGetter.Get(), "SwayingObjectsConverter.cs");
                AssetDatabase.MoveAsset(oldPath: path, newPath: path + ".bak");
            }
        }

        /// <summary>
        /// キャラクターに関する情報を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="defaultAnimationSet"></param>
        private static void ConvertMeta(GameObject avatar, VRC_AvatarDescriptor.AnimationSet defaultAnimationSet)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            avatarDescriptor.Animations = defaultAnimationSet;
        }

        /// <summary>
        /// <see cref="VRMFirstPerson"/>を基に<see cref="VRC_AvatarDescriptor"/>を設定します。
        /// </summary>
        /// <param name="avatar"></param>
        private static void ConvertVRMFirstPerson(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetOrAddComponent<VRC_AvatarDescriptor>();
            var firstPerson = avatar.GetComponent<VRMFirstPerson>();
            avatarDescriptor.ViewPosition = firstPerson.FirstPersonBone.position + firstPerson.FirstPersonOffset - avatar.transform.localPosition;
        }

        /// <summary>
        /// アバター同士の干渉に関する設定を行います。
        /// </summary>
        /// <remarks>
        /// 参照:
        /// 他人が触れる揺れもの構造 — VRChat KemonoClub Wiki
        /// <http://seesaawiki.jp/vrchat_kemonoclub/d/%c2%be%bf%cd%a4%ac%bf%a8%a4%ec%a4%eb%cd%c9%a4%ec%a4%e2%a4%ce%b9%bd%c2%a4>
        /// </remarks>
        /// <param name="avatar"></param>
        private static void SetCollidersForCollisionWithOtherAvatar(GameObject avatar)
        {
            var animator = avatar.GetComponent<Animator>();

            foreach (HumanBodyBones humanoidBodyBone in ComponentsReplacer.BonesForCollisionWithOtherAvatarOnVirtualCast)
            {
                GameObject bone = animator.GetBoneTransform(humanBoneId: humanoidBodyBone).gameObject;

                var rigidbody = bone.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;

                foreach (VRMSpringBoneColliderGroup colliderGroup in bone.GetComponents<VRMSpringBoneColliderGroup>())
                {
                    foreach (VRMSpringBoneColliderGroup.SphereCollider collider in colliderGroup.Colliders)
                    {
                        var sphereCollider = bone.AddComponent<SphereCollider>();
                        sphereCollider.center = collider.Offset;
                        sphereCollider.radius = collider.Radius;
                    }
                }
            }

            // TODO: 触られる側の設定
        }
    }
}