namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>
/// The stores the agent can open, by name. A store has a single writer, so each is opened once
/// and shared. The agent never supplies a path: it can only pick one of these names.
/// </summary>
public sealed class StoreRegistry : IDisposable
{
    private readonly Dictionary<string, RelateDatabase> _stores = new(StringComparer.Ordinal);
    private readonly bool _ownsDatabases;

    public StoreRegistry(CursorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ownsDatabases = true;
        foreach (var (name, uri) in options.Stores)
        {
            _stores[name] = options.ReadOnly ? RelateDatabase.OpenReadOnly(uri) : RelateDatabase.Open(uri);
        }
    }

    /// <summary>Wrap databases the caller owns (tests, embedding).</summary>
    public StoreRegistry(IReadOnlyDictionary<string, RelateDatabase> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        foreach (var (name, db) in stores)
        {
            _stores[name] = db;
        }
    }

    public IReadOnlyCollection<string> Names => _stores.Keys;

    public RelateDatabase Get(string name) => _stores[name];

    /// <summary>Resolve the store an <c>open</c> call asked for; null picks the only store.</summary>
    public (string Name, RelateDatabase Database) Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (_stores.Count == 1)
            {
                var only = _stores.First();
                return (only.Key, only.Value);
            }

            if (_stores.TryGetValue(CursorOptions.DefaultStoreName, out var fallback))
            {
                return (CursorOptions.DefaultStoreName, fallback);
            }

            throw new CursorException("store_required", $"Several stores are configured; pass one of: {string.Join(", ", Names)}.");
        }

        return _stores.TryGetValue(name, out var db)
            ? (name, db)
            : throw new CursorException("unknown_store", $"No store named '{name}'. Configured stores: {string.Join(", ", Names)}.");
    }

    public void Dispose()
    {
        if (!_ownsDatabases)
        {
            return;
        }

        foreach (var db in _stores.Values)
        {
            db.Dispose();
        }
    }
}
