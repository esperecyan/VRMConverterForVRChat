using VRM;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// Humanoidボーンに関する情報。
    /// </summary>
    public class BoneInfo
    {
        /// <summary>
        /// アバターの情報。
        /// </summary>
        public readonly VRMMeta VRMMeta;

        public BoneInfo(VRMMeta vrmMeta)
        {
            this.VRMMeta = vrmMeta;
        }
    }
}
