using System;
using System.Collections.Generic;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
#pragma warning disable
    /// <summary>
    /// .NET Standard 2.1のpolyfill。
    /// </summary>
    /// <remarks>
    /// 複合型の分解 - C# によるプログラミング入門 | ++C++; // 未確認飛行 C
    /// <https://ufcpp.net/study/csharp/datatype/deconstruction/#arbitrary-types>
    ///
    /// Apache License 2.0 (Apache-2.0)
    /// <https://github.com/ufcpp/UfcppSample/blob/8f248a22157878ab496bfca83640cdc4bd1d00e8/LICENSE>
    /// </remarks>
    static class Deconstruction
    {
        public static void Deconstruct<T, U>(this KeyValuePair<T, U> pair, out T key, out U value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static void Deconstruct<T1, T2>(this Tuple<T1, T2> x, out T1 item1, out T2 item2)
        {
            item1 = x.Item1;
            item2 = x.Item2;
        }
    }
#pragma warning restore
}
