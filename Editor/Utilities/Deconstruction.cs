using System.Collections.Generic;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    internal static class Deconstructor
    {
        internal static void Deconstruct<TKey, TValue>(
            this KeyValuePair<TKey, TValue> pair,
            out TKey key,
            out TValue value
        )
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
