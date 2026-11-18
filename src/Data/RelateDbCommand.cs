using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Ambystech.Relate.Data;

/// <summary>
/// A Cypher command. Parameters bind <c>$name</c> placeholders; a parameter may be named
/// <c>name</c>, <c>$name</c> or <c>@name</c>.
/// </summary>
public sealed class RelateDbCommand : DbCommand
{
    private string _commandText = string.Empty;
    private RelateDbConnection? _connection;

    /// <summary>An empty command.</summary>
    public RelateDbCommand() { }

    /// <summary>A command with text, optionally bound to a connection.</summary>
    public RelateDbCommand(string commandText, RelateDbConnection? connection = null)
    {
        _commandText = commandText;
        _connection = connection;
    }

    /// <inheritdoc />
    [AllowNull]
    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }

    /// <summary>Ignored: the embedded engine has no command timeout through the C ABI.</summary>
    public override int CommandTimeout { get; set; }

    /// <summary>Only <see cref="CommandType.Text"/> is supported.</summary>
    public override CommandType CommandType
    {
        get => CommandType.Text;
        set
        {
            if (value != CommandType.Text)
            {
                throw new NotSupportedException("Relate commands are Cypher text.");
            }
        }
    }

    /// <inheritdoc />
    public override bool DesignTimeVisible { get; set; }

    /// <inheritdoc />
    public override UpdateRowSource UpdatedRowSource { get; set; }

    /// <inheritdoc />
    public new RelateDbParameterCollection Parameters { get; } = new();

    /// <inheritdoc />
    protected override DbParameterCollection DbParameterCollection => Parameters;

    /// <inheritdoc />
    protected override DbConnection? DbConnection
    {
        get => _connection;
        set => _connection = (RelateDbConnection?)value;
    }

    /// <summary>
    /// Informational: statements always run in the connection's open transaction, if any,
    /// because a Relate session has one transaction at a time.
    /// </summary>
    protected override DbTransaction? DbTransaction { get; set; }

    /// <inheritdoc />
    public override void Cancel()
    {
        // Statements run synchronously in-process and cannot be interrupted through the C ABI.
    }

    /// <summary>No-op: Relate caches plans itself.</summary>
    public override void Prepare() { }

    /// <summary>
    /// Run the statement and return the number of graph changes: nodes and relationships created or
    /// deleted plus properties set. Returns -1 for statements that changed nothing.
    /// </summary>
    public override int ExecuteNonQuery()
    {
        var counters = Execute().Summary.Counters;
        var changes = counters.NodesCreated + counters.NodesDeleted + counters.RelationshipsCreated
            + counters.RelationshipsDeleted + counters.PropertiesSet;
        return changes > 0 ? (int)Math.Min(changes, int.MaxValue) : -1;
    }

    /// <summary>The first column of the first row, or null.</summary>
    public override object? ExecuteScalar()
    {
        var result = Execute();
        return result.Count > 0 && result.Keys.Count > 0 ? result[0][0] ?? DBNull.Value : null;
    }

    /// <summary>Run the statement and read the rows.</summary>
    public new RelateDbDataReader ExecuteReader() => (RelateDbDataReader)ExecuteDbDataReader(CommandBehavior.Default);

    /// <inheritdoc />
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var reader = new RelateDbDataReader(Execute(), behavior.HasFlag(CommandBehavior.CloseConnection) ? _connection : null);
        return reader;
    }

    /// <inheritdoc />
    protected override DbParameter CreateDbParameter() => new RelateDbParameter();

    /// <summary>Create a parameter.</summary>
    public new RelateDbParameter CreateParameter() => (RelateDbParameter)CreateDbParameter();

    /// <summary>Run the statement and return the driver result (counters, plan, notifications...).</summary>
    public RelateResult Execute()
    {
        var connection = _connection ?? throw new InvalidOperationException("The command has no connection.");
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The connection is not open.");
        }

        if (string.IsNullOrWhiteSpace(_commandText))
        {
            throw new InvalidOperationException("CommandText is empty.");
        }

        IReadOnlyDictionary<string, object?>? parameters = Parameters.Count == 0 ? null : Parameters.ToDictionary();
        return connection.CurrentTransaction is { IsCompleted: false } tx
            ? tx.Transaction.Run(_commandText, parameters)
            : connection.Session.Run(_commandText, parameters);
    }
}
