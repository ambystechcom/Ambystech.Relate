using System.Data.Common;

namespace Ambystech.Relate.Data;

/// <summary>
/// The provider factory. Register with
/// <c>DbProviderFactories.RegisterFactory(RelateDbProviderFactory.InvariantName, RelateDbProviderFactory.Instance)</c>.
/// </summary>
public sealed class RelateDbProviderFactory : DbProviderFactory
{
    /// <summary>The provider invariant name.</summary>
    public const string InvariantName = "Ambystech.Relate";

    /// <summary>The singleton factory.</summary>
    public static readonly RelateDbProviderFactory Instance = new();

    private RelateDbProviderFactory() { }

    /// <inheritdoc />
    public override DbConnection CreateConnection() => new RelateDbConnection();

    /// <inheritdoc />
    public override DbCommand CreateCommand() => new RelateDbCommand();

    /// <inheritdoc />
    public override DbParameter CreateParameter() => new RelateDbParameter();

    /// <inheritdoc />
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new RelateConnectionStringBuilder();
}
