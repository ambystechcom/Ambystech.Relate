namespace Ambystech.Relate.Data;

/// <summary>
/// One <see cref="RelateDatabase"/> per persistent store per process, reference-counted across
/// ADO.NET connections. A store has a single writer, so two connection objects to the same
/// file must share one native database instead of opening it twice. <c>:memory:</c> is never
/// shared: each connection gets its own private store, as with SQLite.
/// </summary>
internal static class SharedDatabases
{
    private static readonly object Gate = new();
    private static readonly Dictionary<(string Path, bool ReadOnly), Entry> Open = new();

    internal static RelateDatabase Acquire(string dataSource, bool readOnly)
    {
        if (dataSource == RelateDatabase.InMemory)
        {
            return readOnly
                ? throw new RelateException("An in-memory store cannot be opened read-only.")
                : RelateDatabase.Open(RelateDatabase.InMemory);
        }

        var key = (Path.GetFullPath(dataSource), readOnly);
        lock (Gate)
        {
            if (Open.TryGetValue(key, out var entry))
            {
                entry.References++;
                return entry.Database;
            }

            var database = readOnly ? RelateDatabase.OpenReadOnly(dataSource) : RelateDatabase.Open(dataSource);
            Open[key] = new Entry(database);
            return database;
        }
    }

    internal static void Release(RelateDatabase database)
    {
        if (database.Uri == RelateDatabase.InMemory)
        {
            database.Dispose();
            return;
        }

        var key = (Path.GetFullPath(database.Uri), database.IsReadOnly);
        lock (Gate)
        {
            if (Open.TryGetValue(key, out var entry) && ReferenceEquals(entry.Database, database) && --entry.References == 0)
            {
                Open.Remove(key);
                database.Dispose();
            }
        }
    }

    /// <summary>The number of shared persistent stores currently open (diagnostics and tests).</summary>
    internal static int Count
    {
        get
        {
            lock (Gate)
            {
                return Open.Count;
            }
        }
    }

    private sealed class Entry(RelateDatabase database)
    {
        public RelateDatabase Database { get; } = database;

        public int References { get; set; } = 1;
    }
}
