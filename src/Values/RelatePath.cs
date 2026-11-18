namespace Ambystech.Relate;

/// <summary>A path: <c>n</c> hops have <c>n + 1</c> nodes and <c>n</c> relationships.</summary>
public sealed record RelatePath(IReadOnlyList<RelateNode> Nodes, IReadOnlyList<RelateRelationship> Relationships)
{
    /// <summary>The first node.</summary>
    public RelateNode Start => Nodes[0];

    /// <summary>The last node.</summary>
    public RelateNode End => Nodes[^1];

    /// <summary>The number of hops.</summary>
    public int Length => Relationships.Count;
}
