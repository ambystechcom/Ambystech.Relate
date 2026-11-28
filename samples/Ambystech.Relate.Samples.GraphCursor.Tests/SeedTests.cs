using Ambystech.Relate.Samples.GraphCursor.Seed;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

public class SeedTests
{
    [Fact]
    public void Seed_is_a_realistic_size_and_has_every_entity_kind()
    {
        using var f = new CursorFixture();

        var nodes = f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>();
        Assert.InRange(nodes, 150, 300);

        var labels = f.Data.Run("CALL db.labels()").Select(r => r.Get<string>("label")).ToList();
        foreach (var expected in new[] { "Service", "Team", "Person", "Repo", "Dependency", "CVE", "Incident" })
        {
            Assert.Contains(expected, labels);
        }
    }

    [Fact]
    public void Loading_the_seed_twice_changes_nothing()
    {
        using var f = new CursorFixture();
        var nodes = f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>();
        var rels = f.Data.Run("MATCH ()-[r]->() RETURN count(r) AS c").Scalar<long>();

        Assert.False(SeedLoader.Load(f.Data));

        Assert.Equal(nodes, f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>());
        Assert.Equal(rels, f.Data.Run("MATCH ()-[r]->() RETURN count(r) AS c").Scalar<long>());
    }

    [Fact]
    public void Statements_are_idempotent_when_replayed_directly()
    {
        using var f = new CursorFixture();
        var nodes = f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>();
        var rels = f.Data.Run("MATCH ()-[r]->() RETURN count(r) AS c").Scalar<long>();

        // Bypass the marker guard: every statement must be safe to run again on its own.
        foreach (var statement in SeedLoader.Statements())
        {
            f.Data.Run(statement);
        }

        Assert.Equal(nodes, f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>());
        Assert.Equal(rels, f.Data.Run("MATCH ()-[r]->() RETURN count(r) AS c").Scalar<long>());
    }

    [Fact]
    public void The_planted_chain_from_cve_to_team_exists_and_is_unique()
    {
        using var f = new CursorFixture();

        var teams = f.Data.Run("""
            MATCH (:CVE {id: 'CVE-2026-1234'})-[:AFFECTS]->(:Dependency)<-[:DEPENDS_ON]-(s:Service)-[:OWNED_BY]->(t:Team)
            RETURN s.name AS service, t.name AS team
            """).ToList();

        var row = Assert.Single(teams);
        Assert.Equal("payments-gateway", row["service"]);
        Assert.Equal("Payments", row["team"]);
    }
}
