using System.Collections.Generic;
using VRM;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    /// <summary>
    /// <see cref="BlendShapeClip.Values">にシェイプキーのパスとインデックスを保持する代わりに、シェイプキー名を保持するクラス。
    /// </summary>
    public class VRMBlendShapeClip : BlendShapeClip
    {
        internal IDictionary<string, float> ShapeKeyValues = new Dictionary<string, float>();
    }
}
