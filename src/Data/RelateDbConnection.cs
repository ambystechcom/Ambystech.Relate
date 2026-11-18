using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Ambystech.Relate.Data;

/// <summary>
/// ADO.NET connection to a Relate store. Each open connection is one <see cref="RelateSession"/>
/// on a store shared per process (single-writer stores are opened once).
/// </summary>
public sealed class RelateDbConnection : DbConnection
{
    private string _connectionString = string.Empty;
    private RelateDatabase? _database;
    private RelateSession? _session;
    private ConnectionState _state = ConnectionState.Closed;

    /// <summary>A closed connection with no connection string (in-memory on open).</summary>
    public RelateDbConnection() { }

    /// <summary>A closed connection, e.g. <c>Data Source=./graph.rl</c>.</summary>
    public RelateDbConnection(string connectionString) => _connectionString = connectionString;

    /// <inheritdoc />
    [AllowNull]
    public override string ConnectionString
    {
        get => _connectionString;
        set
        {
            if (_state != ConnectionState.Closed)
            {
                throw new InvalidOperationException("The connection string cannot change while the connection is open.");
            }

            _connectionString = value ?? string.Empty;
        }
    }

    /// <summary>The session namespace (<c>neo4j</c> unless set).</summary>
    public override string Database => Builder.Namespace ?? "neo4j";

    /// <inheritdoc />
    public override string DataSource => Builder.DataSource;

    /// <inheritdoc />
    public override string ServerVersion => $"Relate C ABI v{RelateDatabase.AbiVersion}";

    /// <inheritdoc />
    public override ConnectionState State => _state;

    /// <summary>The underlying driver session while open, for APIs ADO.NET does not cover.</summary>
    public RelateSession Session => _session ?? throw new InvalidOperationException("The connection is not open.");

    internal RelateDbTransaction? CurrentTransaction { get; set; }

    private RelateConnectionStringBuilder Builder => new(_connectionString);

    /// <inheritdoc />
    public override void Open()
    {
        if (_state == ConnectionState.Open)
        {
            return;
        }

        var builder = Builder;
        var database = SharedDatabases.Acquire(builder.DataSource, builder.Mode == RelateOpenMode.ReadOnly);
        try
        {
            _session = database.OpenSession(builder.Namespace);
            _database = database;
        }
        catch
        {
            SharedDatabases.Release(database);
            throw;
        }

        SetState(ConnectionState.Open);
    }

    /// <inheritdoc />
    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Open();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Close()
    {
        if (_state == ConnectionState.Closed)
        {
            return;
        }

        CurrentTransaction = null;
        _session?.Dispose();
        _session = null;
        if (_database is not null)
        {
            SharedDatabases.Release(_database);
            _database = null;
        }

        SetState(ConnectionState.Closed);
    }

    /// <summary>Switch the session namespace.</summary>
    public override void ChangeDatabase(string databaseName) => Session.SetNamespace(databaseName);

    /// <inheritdoc />
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        if (isolationLevel is not (IsolationLevel.Unspecified or IsolationLevel.Snapshot or IsolationLevel.Serializable))
        {
            throw new NotSupportedException($"Relate transactions are optimistic snapshots; isolation level {isolationLevel} is not supported.");
        }

        if (CurrentTransaction is { IsCompleted: false })
        {
            throw new InvalidOperationException("A transaction is already open on this connection.");
        }

        CurrentTransaction = new RelateDbTransaction(this, Session.BeginTransaction(), isolationLevel);
        return CurrentTransaction;
    }

    /// <summary>Begin a transaction.</summary>
    public new RelateDbTransaction BeginTransaction() => (RelateDbTransaction)BeginDbTransaction(IsolationLevel.Unspecified);

    /// <summary>Create a command bound to this connection.</summary>
    public new RelateDbCommand CreateCommand() => new() { Connection = this };

    /// <inheritdoc />
    protected override DbCommand CreateDbCommand() => CreateCommand();

    /// <inheritdoc />
    protected override DbProviderFactory DbProviderFactory => RelateDbProviderFactory.Instance;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    private void SetState(ConnectionState state)
    {
        var previous = _state;
        _state = state;
        if (previous != state)
        {
            OnStateChange(new StateChangeEventArgs(previous, state));
        }
    }
}
