namespace Ambystech.Relate;

/// <summary>A graph node as returned by a query.</summary>
/// <param name="ElementId">The engine's <c>elementId()</c> string. Prefer it over <paramref name="Id"/>.</param>
/// <param name="Id">The legacy numeric id, as Cypher <c>id()</c> returns it.</param>
/// <param name="Labels">The node's labels.</param>
/// <param name="Properties">The node's properties, in engine order.</param>
public sealed record RelateNode(
    string ElementId,
    long Id,
    IReadOnlyList<string> Labels,
    IReadOnlyDictionary<string, object?> Properties)
{
    /// <summary>Read a property converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(Properties[key]);

    /// <summary>True when the node carries <paramref name="label"/>.</summary>
    public bool HasLabel(string label) => Labels.Contains(label, StringComparer.Ordinal);
}
