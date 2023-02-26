using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace VstuBridgeDebugAdapter.Helpers;

static class Utf8Helper
{
    internal static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
            return 0;

        var first = needle[0];
        var start = 0;
        int i;
        while ((i = haystack[start..].IndexOf(first)) >= 0)
        {
            if (i + needle.Length > haystack.Length)
                return -1;
            start += i;
            if (haystack.Slice(start, needle.Length).SequenceEqual(needle))
                return start;

            start += i + 1;
        }

        return -1;
    }

    internal static Span<byte> Trim(Span<byte> span)
        => TrimStart(TrimEnd(span));

    internal static bool EqualsIgnoreCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            return false;

        for (var i = 0; i < a.Length; i++)
        {
            if (ToUpper(a[i]) != ToUpper(b[i]))
                return false;
        }

        return true;
    }

    internal static byte ToUpper(byte b)
    {
        return b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - (byte)'a' + (byte)'A') : b;
    }

    internal static Span<byte> TrimStart(Span<byte> span)
    {
        var i = 0;
        while (i < span.Length && span[i] == (byte)' ')
            i++;
        return span[i..];
    }

    internal static Span<byte> TrimEnd(Span<byte> span)
    {
        var i = span.Length - 1;
        while (i >= 0 && span[i] == (byte)' ')
            i--;
        return span[..(i + 1)];
    }
}
