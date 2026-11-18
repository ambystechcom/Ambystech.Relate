using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class DatabaseTests
{
    [Fact]
    public void Creates_and_reads_back_a_graph_with_counters()
    {
        using var db = RelateDatabase.Open();
        var write = db.Run(TestData.Team);

        Assert.Equal(RelateQueryType.Write, write.Summary.QueryType);
        Assert.Equal(5, write.Summary.Counters.NodesCreated);
        Assert.Equal(5, write.Summary.Counters.RelationshipsCreated);
        Assert.True(write.Summary.Counters.ContainsUpdates);
        Assert.NotNull(write.Summary.Bookmark);
        Assert.Equal("neo4j", write.Summary.Namespace);

        var read = db.Run("""
            MATCH (p:Person)-[w:WORKS_ON]->(:Project {name: $project})
            RETURN p.name AS name, w.since AS since ORDER BY since
            """, new Dictionary<string, object?> { ["project"] = "relate" });

        Assert.Equal(["name", "since"], read.Keys);
        Assert.Equal(2, read.Count);
        Assert.Equal("Grace", read[0]["name"]);
        Assert.Equal(2023L, read[0]["since"]);
        Assert.Equal(2024, read[1].Get<int>("since"));
        Assert.Equal(RelateQueryType.Read, read.Summary.QueryType);
    }

    [Fact]
    public void Anonymous_object_parameters_bind_by_property_name()
    {
        using var db = RelateDatabase.Open();
        db.Run(TestData.Team);

        var count = db.Run("MATCH (p:Person {role: $role}) RETURN count(p) AS n", new { role = "engineer" }).Scalar<long>();

        Assert.Equal(2, count);
    }

    [Fact]
    public void Explain_returns_a_plan_document()
    {
        using var db = RelateDatabase.Open();
        var result = db.Run("EXPLAIN MATCH (a:Person), (b:Project) RETURN a, b");

        Assert.NotNull(result.Summary.PlanJson);
        Assert.Contains("relate.plan/v1", result.Summary.PlanJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void File_store_persists_and_reopens_read_only()
    {
        var dir = Directory.CreateTempSubdirectory("relate-");
        var file = Path.Combine(dir.FullName, "graph.rl");
        try
        {
            using (var db = RelateDatabase.Open(file))
            {
                db.Run(TestData.Team);
            }

            using (var ro = RelateDatabase.OpenReadOnly(file))
            {
                Assert.True(ro.IsReadOnly);
                Assert.Equal(5L, ro.Run("MATCH (n) RETURN count(n) AS n").Scalar<long>());
                Assert.ThrowsAny<RelateException>(() => ro.Run("CREATE (:Nope)"));
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Second_writer_on_the_same_file_is_refused()
    {
        var dir = Directory.CreateTempSubdirectory("relate-");
        var file = Path.Combine(dir.FullName, "graph.rl");
        try
        {
            using var first = RelateDatabase.Open(file);
            first.Run("CREATE (:A)");
            Assert.ThrowsAny<RelateException>(() => RelateDatabase.Open(file));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Session_survives_database_dispose_until_closed()
    {
        var db = RelateDatabase.Open();
        var session = db.OpenSession();
        db.Dispose();

        // The native store is held open by the session's reference.
        Assert.Equal(1L, session.Run("RETURN 1 AS one").Scalar<long>());
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Run("RETURN 1"));
    }

    [Fact]
    public async Task Sessions_run_in_parallel()
    {
        using var db = RelateDatabase.Open();
        db.Run("UNWIND range(1, 100) AS i CREATE (:N {i: i})");

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var session = db.OpenSession();
            var r = await session.RunAsync("MATCH (n:N) RETURN sum(n.i) AS s");
            return r.Scalar<long>();
        });

        var sums = await Task.WhenAll(tasks);
        Assert.All(sums, s => Assert.Equal(5050L, s));
    }
}
