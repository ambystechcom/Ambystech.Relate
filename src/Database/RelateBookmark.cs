using System.Diagnostics.CodeAnalysis;

namespace Ambystech.Relate;

/// <summary>Helpers for Relate bookmarks (<c>relate:&lt;n&gt;</c>).</summary>
public static class RelateBookmark
{
    /// <summary>
    /// The commit sequence <c>n</c> of a bookmark. Compare sequences, never the strings:
    /// <c>relate:10</c> sorts before <c>relate:9</c> as text.
    /// </summary>
    public static long Sequence(string bookmark)
    {
        ArgumentException.ThrowIfNullOrEmpty(bookmark);
        const string prefix = "relate:";
        return bookmark.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(bookmark.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : throw new FormatException($"'{bookmark}' is not a Relate bookmark.");
    }
}
