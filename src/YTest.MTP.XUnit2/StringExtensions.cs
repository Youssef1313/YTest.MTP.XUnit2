#if NETFRAMEWORK

using System;

namespace YTest.MTP.XUnit2;

internal static class StringExtensions
{
    public static bool Contains(this string s, string value, StringComparison comparison)
        => s.IndexOf(value, comparison) >= 0;
}
#endif
