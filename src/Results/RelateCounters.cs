using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>The canonical mutation counters (ADR-0216).</summary>
public sealed record RelateCounters(
    long NodesCreated,
    long NodesDeleted,
    long RelationshipsCreated,
    long RelationshipsDeleted,
    long PropertiesSet,
    long LabelsAdded,
    long LabelsRemoved,
    long IndexesAdded,
    long IndexesRemoved,
    long ConstraintsAdded,
    long ConstraintsRemoved)
{
    /// <summary>True when any counter is non-zero.</summary>
    public bool ContainsUpdates =>
        NodesCreated + NodesDeleted + RelationshipsCreated + RelationshipsDeleted + PropertiesSet + LabelsAdded + LabelsRemoved
        + IndexesAdded + IndexesRemoved + ConstraintsAdded + ConstraintsRemoved > 0;

    internal static readonly RelateCounters Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    internal static RelateCounters Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Empty;
        }

        using var doc = JsonDocument.Parse(json);
        var e = doc.RootElement;
        long Get(string name) => e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
        return new RelateCounters(
            Get("nodes_created"), Get("nodes_deleted"), Get("relationships_created"), Get("relationships_deleted"),
            Get("properties_set"), Get("labels_added"), Get("labels_removed"), Get("indexes_added"),
            Get("indexes_removed"), Get("constraints_added"), Get("constraints_removed"));
    }
}
