using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>
/// An open Relate store, running in-process. Thread-safe: open one per store and share it;
/// open a <see cref="RelateSession"/> per concurrent unit of work.
/// </summary>
/// <remarks>
/// A persistent store has a single writer: a second writable open of the same path (from this
/// or another process) fails with a locked-store error. Within one process, share this instance.
/// </remarks>
public sealed class RelateDatabase : IDisposable
{
    /// <summary>The URI of a private, temporary in-memory store.</summary>
    public const string InMemory = ":memory:";

    private readonly DatabaseHandle _handle;
    private readonly Lazy<RelateSession> _defaultSession;

    private RelateDatabase(DatabaseHandle handle, string uri, bool readOnly)
    {
        _handle = handle;
        Uri = uri;
        IsReadOnly = readOnly;
        _defaultSession = new Lazy<RelateSession>(() => OpenSession(), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The store this database opened: <c>:memory:</c>, a <c>.rl</c> file or a directory.</summary>
    public string Uri { get; }

    /// <summary>True when opened read-only: writes and checkpoints are refused.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The native engine's C ABI version.</summary>
    public static int AbiVersion
    {
        get
        {
            NativeLoader.EnsureLoaded();
            return RelateNative.relate_abi_version();
        }
    }

    /// <summary>
    /// Open (or create) a store: <c>:memory:</c>, a path ending in <c>.rl</c> (single file),
    /// or a directory path.
    /// </summary>
    public static RelateDatabase Open(string uri = InMemory)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        NativeLoader.EnsureLoaded();
        var status = RelateNative.relate_database_open(uri, out var db, out var err);
        NativeCall.Check(status, err, $"Opening '{uri}'");
        return new RelateDatabase(new DatabaseHandle(db), uri, readOnly: false);
    }

    /// <summary>Open an existing store read-only. A missing store is an error, not created.</summary>
    public static RelateDatabase OpenReadOnly(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        NativeLoader.EnsureLoaded();
        var status = RelateNative.relate_database_open_read_only(uri, out var db, out var err);
        NativeCall.Check(status, err, $"Opening '{uri}' read-only");
        return new RelateDatabase(new DatabaseHandle(db), uri, readOnly: true);
    }

    /// <summary>
    /// Open a session. <paramref name="namespace"/> is <c>neo4j</c> (default), <c>system</c>
    /// or <c>system_ontologies</c>.
    /// </summary>
    public RelateSession OpenSession(string? @namespace = null)
    {
        using var db = NativeCall.Borrow(_handle, nameof(RelateDatabase));
        var status = RelateNative.relate_connect_namespace(db.Value, @namespace, out var conn, out var err);
        NativeCall.Check(status, err, "Connecting");
        return new RelateSession(this, new ConnectionHandle(conn));
    }

    /// <summary>
    /// Run one statement on the database's shared default session (auto-commit).
    /// Convenient for scripts; use <see cref="OpenSession"/> for concurrent work.
    /// </summary>
    public RelateResult Run(string cypher, IReadOnlyDictionary<string, object?>? parameters = null) =>
        _defaultSession.Value.Run(cypher, parameters);

    /// <summary>Run one statement on the default session, with parameters from an object's public properties.</summary>
    public RelateResult Run<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TParameters>(
        string cypher, TParameters parameters)
        where TParameters : class =>
        _defaultSession.Value.Run(cypher, parameters);

    /// <summary>Force a checkpoint: fold the write-ahead log into the store's snapshot.</summary>
    public void Checkpoint()
    {
        using var db = NativeCall.Borrow(_handle, nameof(RelateDatabase));
        NativeCall.Check(RelateNative.relate_checkpoint(db.Value, out var err), err, "Checkpoint");
    }

    /// <summary>Close the store. Sessions opened from it stop working.</summary>
    public void Dispose()
    {
        if (_defaultSession.IsValueCreated)
        {
            _defaultSession.Value.Dispose();
        }

        _handle.Dispose();
    }

    internal DatabaseHandle Handle => _handle;
}
