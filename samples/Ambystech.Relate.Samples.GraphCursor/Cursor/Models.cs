namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>A node as the agent sees it: its id, labels and a few key properties.</summary>
public sealed record NodeView(string Id, IReadOnlyList<string> Labels, IReadOnlyDictionary<string, object?> Props);

/// <summary>How many relationships of a type lead to nodes with the given labels.</summary>
public sealed record RelCount(string RelType, string Direction, IReadOnlyList<string> TargetLabels, long Count);

/// <summary>The labels, relationship types and property keys within one hop of the current position.</summary>
public sealed record Vocabulary(IReadOnlyList<string> Labels, IReadOnlyList<string> RelTypes, IReadOnlyList<string> PropertyKeys);

public sealed record OpenResult(
    string CursorId,
    string Store,
    IReadOnlyList<NodeView> Position,
    IReadOnlyList<RelCount> Outgoing,
    IReadOnlyList<RelCount> Incoming,
    Vocabulary Vocabulary);

public sealed record ChoiceView(
    string ChoiceId,
    string RelType,
    string Direction,
    IReadOnlyList<string> TargetLabels,
    long Count,
    IReadOnlyList<NodeView> Samples,
    double Score,
    bool Backtrack);

public sealed record NextHopResult(string CursorId, string NodeId, string Goal, IReadOnlyList<ChoiceView> Choices);

/// <summary>One turn of the walk. Moving to a choice's whole target set is one step with several targets.</summary>
public sealed record Step(
    int Seq,
    string From,
    string RelType,
    string Direction,
    IReadOnlyList<string> To,
    double Score,
    DateTimeOffset At);

public sealed record TakeResult(
    string CursorId,
    IReadOnlyList<NodeView> Position,
    Step Step,
    IReadOnlyList<RelCount> Outgoing,
    IReadOnlyList<RelCount> Incoming);

public sealed record GatherResult(string CursorId, string Scope, IReadOnlyList<NodeView> Nodes, bool Truncated, int Bytes);

public sealed record EvidenceItem(string Scope, IReadOnlyList<NodeView> Nodes, bool Truncated);

public sealed record WalkResult(
    string CursorId,
    string Store,
    string? Goal,
    IReadOnlyList<string> Start,
    IReadOnlyList<Step> Steps,
    IReadOnlyList<NodeView> Position,
    IReadOnlyList<EvidenceItem> Evidence);

public sealed record RawQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated,
    double ElapsedMilliseconds);

/// <summary>A hop option the cursor has offered, remembered so <c>take</c> can reject anything else.</summary>
internal sealed record OfferedChoice(
    string ChoiceId,
    string FromId,
    string RelType,
    string Direction,
    IReadOnlyList<string> TargetLabels,
    long Count,
    IReadOnlyList<string> SampleIds,
    double Score);

/// <summary>A tool failure with a stable code the agent can act on.</summary>
public sealed class CursorException : Exception
{
    public CursorException(string code, string message)
        : base($"{code}: {message}") => Code = code;

    public string Code { get; }
}
