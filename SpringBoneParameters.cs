
namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// <see cref="VRM.VRMSpringBone"/>の各種パラメータに対応する値。
    /// </summary>
    public class SpringBoneParameters
    {
        public readonly float StiffnessForce = 1.0f;
        public readonly float DragForce = 0.4f;
        
        public SpringBoneParameters(float stiffnessForce, float dragForce)
        {
            this.StiffnessForce = stiffnessForce;
            this.DragForce = dragForce;
        }
    }
}
