using System.Globalization;
using System.Text.RegularExpressions;

namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>
/// The restricted start query: a single node match, <c>(:Label {key: 'value', n: 1})</c>. The server parses it
/// and rebuilds a parameterized query, so the agent never writes Cypher unless raw queries are enabled.
/// </summary>
public static partial class StartQueryParser
{
    public const string Form = "(:Label {key: 'value'}), a single node pattern with optional string, number or boolean properties";

    public static bool TryParse(string text, out string label, out IReadOnlyDictionary<string, object?> where)
    {
        label = string.Empty;
        where = new Dictionary<string, object?>();

        var match = Pattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (match.Groups["props"].Success)
        {
            foreach (Match entry in Entry().Matches(match.Groups["props"].Value))
            {
                props[entry.Groups["key"].Value] = Convert(entry.Groups["value"].Value);
            }

            // Anything the entry pattern did not consume means the property list was not in the allowed form.
            var consumed = Entry().Replace(match.Groups["props"].Value, string.Empty).Replace(",", string.Empty, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(consumed))
            {
                return false;
            }
        }

        label = match.Groups["label"].Value;
        where = props;
        return true;
    }

    private static object Convert(string raw)
    {
        if (raw is ['\'', .., '\''] or ['"', .., '"'])
        {
            return raw[1..^1];
        }

        if (raw is "true" or "false")
        {
            return raw == "true";
        }

        return long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            ? n
            : double.Parse(raw, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^\(?\s*:?\s*(?<label>[A-Za-z_][A-Za-z0-9_]*)\s*(\{(?<props>[^{}]*)\})?\s*\)?$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>'[^'\\]*'|""[^""\\]*""|-?\d+(\.\d+)?|true|false)\s*")]
    private static partial Regex Entry();
}
