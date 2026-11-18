using System.Data;
using System.Data.Common;
using Ambystech.Relate.Data;

namespace Ambystech.Relate.Tests;

public class AdoNetTests
{
    [Fact]
    public void Command_reader_and_scalar()
    {
        using var conn = new RelateDbConnection("Data Source=:memory:");
        conn.Open();
        Assert.Equal(ConnectionState.Open, conn.State);

        using (var create = conn.CreateCommand())
        {
            create.CommandText = "UNWIND $names AS name CREATE (:Person {name: name, age: size(name)})";
            create.Parameters.AddWithValue("names", new[] { "Ada", "Grace", "Noor" });
            Assert.Equal(9, create.ExecuteNonQuery()); // 3 nodes + 6 properties
        }

        using var cmd = new RelateDbCommand("MATCH (p:Person) WHERE p.age >= $min RETURN p.name AS name, p.age AS age ORDER BY name", conn);
        cmd.Parameters.Add(new RelateDbParameter("@min", 4));

        using var reader = cmd.ExecuteReader();
        Assert.Equal(2, reader.FieldCount);
        Assert.Equal("name", reader.GetName(0));
        Assert.Equal(typeof(long), reader.GetFieldType(1));

        var rows = new List<(string, int)>();
        while (reader.Read())
        {
            rows.Add((reader.GetString(reader.GetOrdinal("name")), reader.GetInt32(1)));
        }

        Assert.Equal([("Grace", 5), ("Noor", 4)], rows);

        using var scalar = new RelateDbCommand("MATCH (p:Person) RETURN count(p)", conn);
        Assert.Equal(3L, scalar.ExecuteScalar());
    }

    [Fact]
    public void Nulls_read_as_DBNull()
    {
        using var conn = new RelateDbConnection();
        conn.Open();
        using var cmd = new RelateDbCommand("RETURN null AS x, 1 AS y", conn);
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        Assert.Equal(DBNull.Value, reader.GetValue(0));
        Assert.Equal(1L, reader.GetFieldValue<long>(1));
        Assert.False(reader.Read());
    }

    [Fact]
    public void Transactions_commit_and_roll_back()
    {
        using var conn = new RelateDbConnection();
        conn.Open();

        using (var tx = conn.BeginTransaction())
        {
            new RelateDbCommand("CREATE (:T {v: 1})", conn).ExecuteNonQuery();
            tx.Commit();
            Assert.NotNull(tx.Bookmark);
        }

        using (var tx = conn.BeginTransaction())
        {
            new RelateDbCommand("CREATE (:T {v: 2})", conn).ExecuteNonQuery();
            tx.Rollback();
        }

        using (conn.BeginTransaction())
        {
            new RelateDbCommand("CREATE (:T {v: 3})", conn).ExecuteNonQuery();
            // disposed -> rollback
        }

        Assert.Equal(1L, new RelateDbCommand("MATCH (t:T) RETURN count(t)", conn).ExecuteScalar());
    }

    [Fact]
    public void Connections_to_the_same_file_share_one_store()
    {
        var dir = Directory.CreateTempSubdirectory("relate-");
        var cs = new RelateConnectionStringBuilder { DataSource = Path.Combine(dir.FullName, "shared.rl") }.ConnectionString;
        try
        {
            using (var a = new RelateDbConnection(cs))
            using (var b = new RelateDbConnection(cs))
            {
                a.Open();
                b.Open(); // would be "storage locked" if each opened its own native store
                Assert.Equal(1, SharedDatabases.Count);

                new RelateDbCommand("CREATE (:Shared)", a).ExecuteNonQuery();
                Assert.Equal(1L, new RelateDbCommand("MATCH (s:Shared) RETURN count(s)", b).ExecuteScalar());
            }

            Assert.Equal(0, SharedDatabases.Count);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Provider_factory_and_connection_string_builder()
    {
        DbProviderFactories.RegisterFactory(RelateDbProviderFactory.InvariantName, RelateDbProviderFactory.Instance);
        var factory = DbProviderFactories.GetFactory(RelateDbProviderFactory.InvariantName);

        var builder = (RelateConnectionStringBuilder)factory.CreateConnectionStringBuilder()!;
        builder.DataSource = "./graph.rl";
        builder.Mode = RelateOpenMode.ReadOnly;
        builder.Namespace = "neo4j";

        var parsed = new RelateConnectionStringBuilder(builder.ConnectionString);
        Assert.Equal("./graph.rl", parsed.DataSource);
        Assert.Equal(RelateOpenMode.ReadOnly, parsed.Mode);
        Assert.Equal("neo4j", parsed.Namespace);

        using var conn = factory.CreateConnection()!;
        Assert.IsType<RelateDbConnection>(conn);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "RETURN 41 + 1";
        Assert.Equal(42L, cmd.ExecuteScalar());
    }
}
