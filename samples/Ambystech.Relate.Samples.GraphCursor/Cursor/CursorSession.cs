namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>The state behind one cursor id. All access goes through <see cref="Gate"/>.</summary>
internal sealed class CursorSession
{
    public CursorSession(string id, string store, RelateDatabase database, DateTimeOffset startedAt)
    {
        Id = id;
        Store = store;
        Database = database;
        StartedAt = startedAt;
    }

    public string Id { get; }

    public string Store { get; }

    public RelateDatabase Database { get; }

    public DateTimeOffset StartedAt { get; }

    public object Gate { get; } = new();

    /// <summary>The node set the cursor is standing on.</summary>
    public List<string> Position { get; set; } = [];

    public List<string> Start { get; } = [];

    /// <summary>Choices offered from the current position; cleared when the cursor moves.</summary>
    public Dictionary<string, OfferedChoice> Offered { get; } = new(StringComparer.Ordinal);

    /// <summary>Every node id this cursor has ever returned to the agent. <c>take</c> accepts nothing else.</summary>
    public HashSet<string> Issued { get; } = new(StringComparer.Ordinal);

    /// <summary>Labels the agent has seen, the only ones <c>gather label:&lt;L&gt;</c> accepts.</summary>
    public HashSet<string> SeenLabels { get; } = new(StringComparer.Ordinal);

    public List<Step> Steps { get; } = [];

    public List<EvidenceItem> Evidence { get; } = [];

    public string? Goal { get; set; }

    public int ChoiceCounter { get; set; }

    public int Seq { get; set; }
}
