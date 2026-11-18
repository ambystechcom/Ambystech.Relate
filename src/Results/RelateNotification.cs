using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>A planner notification (ADR-0363), e.g. a Cartesian product warning.</summary>
public sealed record RelateNotification(
    string Code,
    string? GqlStatus,
    string Severity,
    string Category,
    string Title,
    string Description,
    string? Remedy,
    long? OperatorId)
{
    internal static IReadOnlyList<RelateNotification> ParseAll(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        var list = new List<RelateNotification>();
        foreach (var n in doc.RootElement.EnumerateArray())
        {
            string? S(string name) => n.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            list.Add(new RelateNotification(
                S("code") ?? "",
                S("gql_status"),
                S("severity") ?? "",
                S("category") ?? "",
                S("title") ?? "",
                S("description") ?? "",
                S("remedy"),
                n.TryGetProperty("operator_id", out var op) && op.TryGetInt64(out var id) ? id : null));
        }

        return list;
    }
}
