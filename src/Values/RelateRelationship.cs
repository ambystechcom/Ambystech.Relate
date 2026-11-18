namespace Ambystech.Relate;

/// <summary>A graph relationship as returned by a query.</summary>
public sealed record RelateRelationship(
    string ElementId,
    long Id,
    string StartElementId,
    long StartId,
    string EndElementId,
    long EndId,
    string Type,
    IReadOnlyDictionary<string, object?> Properties)
{
    /// <summary>Read a property converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(Properties[key]);
}
