using System.Text;

namespace Ambystech.Relate.Interop;

internal static unsafe class NativeStrings
{
    /// <summary>Copy a result-owned, NUL-terminated UTF-8 string. <c>NULL</c> maps to <c>null</c>.</summary>
    internal static string? FromBorrowed(byte* p) =>
        p == null ? null : Encoding.UTF8.GetString(p, IndexOfNul(p));

    /// <summary>Copy <paramref name="len"/> bytes of UTF-8; interior NULs survive.</summary>
    internal static string FromBorrowed(byte* p, nuint len) =>
        Encoding.UTF8.GetString(p, checked((int)len));

    /// <summary>Copy and free a caller-owned string (error_json, bookmark_out).</summary>
    internal static string? TakeOwned(nint p)
    {
        if (p == 0)
        {
            return null;
        }

        try
        {
            return FromBorrowed((byte*)p);
        }
        finally
        {
            RelateNative.relate_free_string(p);
        }
    }

    private static int IndexOfNul(byte* p)
    {
        var length = 0;
        while (p[length] != 0)
        {
            length++;
        }

        return length;
    }
}
