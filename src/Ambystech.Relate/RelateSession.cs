using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>
/// One session on a <see cref="RelateDatabase"/> (a native connection). Calls are serialized
/// internally, so a session is safe to share, but distinct sessions run in parallel.
/// Statements auto-commit unless run inside <see cref="BeginTransaction"/>.
/// </summary>
public sealed class RelateSession : IDisposable
{
    private readonly ConnectionHandle _handle;
    private readonly object _gate = new();
    private RelateTransaction? _transaction;
    private bool _databaseRefAdded;

    internal RelateSession(RelateDatabase database, ConnectionHandle handle)
    {
        Database = database;
        _handle = handle;

        // Keep the store open while this session lives: the native close is deferred until
        // the last session on it is disposed, so a connection never outlives its database.
        database.Handle.DangerousAddRef(ref _databaseRefAdded);
    }

    /// <summary>The database this session belongs to.</summary>
    public RelateDatabase Database { get; }

    /// <summary>True while an explicit transaction is open.</summary>
    public bool InTransaction
    {
        get
        {
            lock (_gate)
            {
                return _transaction is { IsOpen: true };
            }
        }
    }

    /// <summary>
    /// Change the session namespace (<c>neo4j</c>, <c>system</c>, <c>system_ontologies</c>).
    /// Not allowed while a transaction is open.
    /// </summary>
    public void SetNamespace(string @namespace)
    {
        ArgumentException.ThrowIfNullOrEmpty(@namespace);
        lock (_gate)
        {
            using var conn = NativeCall.Borrow(_handle, nameof(RelateSession));
            NativeCall.Check(RelateNative.relate_conn_set_namespace(conn.Value, @namespace, out var err), err, "Setting the namespace");
        }
    }

    /// <summary>Run one Cypher statement. Parameters bind <c>$name</c> placeholders.</summary>
    public RelateResult Run(string cypher, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cypher);
        using var native = ParamsWriter.Build(parameters);
        lock (_gate)
        {
            return RunLocked(cypher, native);
        }
    }

    /// <summary>Run one statement with parameters taken from an object's public properties (anonymous types welcome).</summary>
    public RelateResult Run<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TParameters>(
        string cypher, TParameters parameters)
        where TParameters : class =>
        Run(cypher, ToDictionary(parameters));

    /// <summary>
    /// Run on a thread-pool thread. The engine is synchronous and in-process; this keeps request
    /// threads free. Cancellation is honoured before the statement starts, not during it.
    /// </summary>
    public Task<RelateResult> RunAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Run(cypher, parameters), cancellationToken);

    /// <summary>Open an explicit transaction. Statements run on this session join it until it ends.</summary>
    public RelateTransaction BeginTransaction()
    {
        lock (_gate)
        {
            if (_transaction is { IsOpen: true })
            {
                throw new InvalidOperationException("A transaction is already open on this session (nested transactions are not supported).");
            }

            using var conn = NativeCall.Borrow(_handle, nameof(RelateSession));
            NativeCall.Check(RelateNative.relate_conn_begin(conn.Value, out var err), err, "Begin");
            _transaction = new RelateTransaction(this);
            return _transaction;
        }
    }

    /// <summary>
    /// Run <paramref name="work"/> in a transaction and commit it, retrying on
    /// <see cref="RelateTransientException"/> (e.g. a write-write conflict) up to <paramref name="maxRetries"/> times.
    /// </summary>
    public T ExecuteWrite<T>(Func<RelateTransaction, T> work, int maxRetries = 3)
    {
        ArgumentNullException.ThrowIfNull(work);
        for (var attempt = 0; ; attempt++)
        {
            using var tx = BeginTransaction();
            try
            {
                var value = work(tx);
                tx.Commit();
                return value;
            }
            catch (RelateTransientException) when (attempt < maxRetries)
            {
                tx.Rollback();
                Thread.Sleep(TimeSpan.FromMilliseconds(10 * (1 << attempt)));
            }
        }
    }

    /// <summary>Run <paramref name="work"/> in a transaction that is rolled back afterwards (read-only intent).</summary>
    public T ExecuteRead<T>(Func<RelateTransaction, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var tx = BeginTransaction();
        try
        {
            return work(tx);
        }
        finally
        {
            tx.Rollback();
        }
    }

    /// <summary>Close the session, rolling back any open transaction.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_transaction is { IsOpen: true })
            {
                try
                {
                    RollbackLocked();
                }
                catch (RelateException)
                {
                    // Disconnect below still frees the native connection.
                }
            }

            if (_handle.IsClosed)
            {
                return;
            }

            _handle.Dispose();
            if (_databaseRefAdded)
            {
                _databaseRefAdded = false;
                Database.Handle.DangerousRelease();
            }
        }
    }

    internal RelateResult RunInTransaction(RelateTransaction tx, string cypher, IReadOnlyDictionary<string, object?>? parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(cypher);
        using var native = ParamsWriter.Build(parameters);
        lock (_gate)
        {
            EnsureCurrent(tx);
            return RunLocked(cypher, native);
        }
    }

    internal string? Commit(RelateTransaction tx)
    {
        lock (_gate)
        {
            EnsureCurrent(tx);
            using var conn = NativeCall.Borrow(_handle, nameof(RelateSession));
            var status = RelateNative.relate_conn_commit(conn.Value, out var bookmark, out var err);
            if (status != RelateNative.RELATE_OK)
            {
                // A refused commit may leave the engine transaction open ("rollback required"); keep ours open too.
                NativeStrings.TakeOwned(bookmark);
                NativeCall.Check(status, err, "Commit");
            }

            NativeStrings.TakeOwned(err);
            tx.MarkClosed();
            return NativeStrings.TakeOwned(bookmark);
        }
    }

    internal void Rollback(RelateTransaction tx)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(tx, _transaction) || !tx.IsOpen)
            {
                return;
            }

            RollbackLocked();
        }
    }

    private void RollbackLocked()
    {
        var tx = _transaction!;
        using var conn = NativeCall.Borrow(_handle, nameof(RelateSession));
        try
        {
            NativeCall.Check(RelateNative.relate_conn_rollback(conn.Value, out var err), err, "Rollback");
        }
        finally
        {
            tx.MarkClosed();
        }
    }

    private RelateResult RunLocked(string cypher, ParamsHandle? native)
    {
        using var conn = NativeCall.Borrow(_handle, nameof(RelateSession));
        var status = RelateNative.relate_query_params(conn.Value, cypher, native?.DangerousGetHandle() ?? 0, out var result, out var err);
        NativeCall.Check(status, err, "Query");
        using var handle = new ResultHandle(result);
        return RelateResult.Materialize(handle);
    }

    private void EnsureCurrent(RelateTransaction tx)
    {
        if (!ReferenceEquals(tx, _transaction) || !tx.IsOpen)
        {
            throw new InvalidOperationException("The transaction is no longer open.");
        }
    }

    internal static IReadOnlyDictionary<string, object?> ToDictionary<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T parameters)
        where T : class
    {
        if (parameters is IReadOnlyDictionary<string, object?> ready)
        {
            return new Dictionary<string, object?>(ready, StringComparer.Ordinal);
        }

        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length == 0)
            {
                dict[prop.Name] = prop.GetValue(parameters);
            }
        }

        return dict;
    }
}
