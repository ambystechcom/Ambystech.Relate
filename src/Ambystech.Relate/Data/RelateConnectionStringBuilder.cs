using System.Data.Common;

namespace Ambystech.Relate.Data;

/// <summary>Whether an ADO.NET connection may write.</summary>
public enum RelateOpenMode
{
    /// <summary>Open (or create) the store for reading and writing.</summary>
    ReadWrite,
    /// <summary>Open an existing store read-only; writes and checkpoints are refused.</summary>
    ReadOnly,
}

/// <summary>
/// Connection string for <see cref="RelateDbConnection"/>, e.g.
/// <c>Data Source=./graph.rl;Mode=ReadOnly;Namespace=neo4j</c>.
/// </summary>
public sealed class RelateConnectionStringBuilder : DbConnectionStringBuilder
{
    private const string DataSourceKey = "Data Source";
    private const string ModeKey = "Mode";
    private const string NamespaceKey = "Namespace";

    /// <summary>An empty builder (in-memory, read-write).</summary>
    public RelateConnectionStringBuilder() { }

    /// <summary>Parse an existing connection string.</summary>
    public RelateConnectionStringBuilder(string? connectionString) => ConnectionString = connectionString ?? string.Empty;

    /// <summary><c>:memory:</c> (default), a <c>.rl</c> file or a store directory. Aliases: DataSource, Database, Filename.</summary>
    public string DataSource
    {
        get => Read(DataSourceKey, "DataSource", "Database", "Filename") ?? RelateDatabase.InMemory;
        set => this[DataSourceKey] = value;
    }

    /// <summary><see cref="RelateOpenMode.ReadWrite"/> (default) or <see cref="RelateOpenMode.ReadOnly"/>.</summary>
    public RelateOpenMode Mode
    {
        get => Enum.TryParse<RelateOpenMode>(Read(ModeKey), ignoreCase: true, out var mode) ? mode : RelateOpenMode.ReadWrite;
        set => this[ModeKey] = value.ToString();
    }

    /// <summary>The session namespace: <c>neo4j</c> (default), <c>system</c> or <c>system_ontologies</c>.</summary>
    public string? Namespace
    {
        get => Read(NamespaceKey);
        set => this[NamespaceKey] = value;
    }

    private string? Read(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetValue(key, out var value) && value is not null && value.ToString() is { Length: > 0 } text)
            {
                return text;
            }
        }

        return null;
    }
}
