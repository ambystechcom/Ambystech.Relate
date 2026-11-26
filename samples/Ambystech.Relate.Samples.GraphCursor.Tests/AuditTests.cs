using Ambystech.Relate.Samples.GraphCursor.Audit;
using Ambystech.Relate.Samples.GraphCursor.Cursor;
using Ambystech.Relate.Samples.GraphCursor.Scoring;
using static Ambystech.Relate.Samples.GraphCursor.Tests.CursorFixture;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

public class AuditTests
{
    private const string Goal = "find which team owns the service exposed to CVE-2026-1234";

    private static (string CursorId, IReadOnlyList<Step> Steps) WalkToTheTeam(CursorService service)
    {
        var open = service.Open(null, PlantedCve);
        var node = open.Position.Single().Id;
        foreach (var (relType, direction) in new[] { ("AFFECTS", "out"), ("DEPENDS_ON", "in"), ("OWNED_BY", "out") })
        {
            var hop = service.NextHop(open.CursorId, node, Goal);
            var taken = service.Take(open.CursorId, Choice(hop, relType, direction).ChoiceId);
            node = taken.Position.Single().Id;
        }

        return (open.CursorId, service.Walk(open.CursorId).Steps);
    }

    [Fact]
    public void Every_hop_is_logged_as_a_walk_graph_in_the_audit_store()
    {
        using var f = new CursorFixture();

        var (cursorId, steps) = WalkToTheTeam(f.Service);

        var walk = f.Audit.Database.Run(
            "MATCH (w:Walk {id: $id}) RETURN w.goal AS goal, w.store AS store, w.startedAt AS startedAt",
            new Dictionary<string, object?> { ["id"] = cursorId }).Single();
        Assert.Equal(Goal, walk["goal"]);
        Assert.Equal("default", walk["store"]);
        Assert.NotNull(walk["startedAt"]);

        var hops = f.Audit.Database.Run(
            "MATCH (:Walk {id: $id})-[s:STEP]->(h:Hop) RETURN count(h) AS c",
            new Dictionary<string, object?> { ["id"] = cursorId }).Scalar<long>();
        Assert.Equal(3, hops);
        Assert.Equal(3, steps.Count);
    }

    [Fact]
    public void The_shipped_query_rebuilds_the_walk_in_order()
    {
        using var f = new CursorFixture();
        var (cursorId, steps) = WalkToTheTeam(f.Service);

        var rebuilt = f.Audit.Rebuild(cursorId);

        Assert.Equal(["AFFECTS", "DEPENDS_ON", "OWNED_BY"], rebuilt.Select(h => h.RelType));
        Assert.Equal([1, 2, 3], rebuilt.Select(h => h.Seq));
        Assert.Equal(steps.Select(s => s.From), rebuilt.Select(h => h.FromId));
        Assert.Equal(steps.Select(s => s.To.Single()), rebuilt.Select(h => h.ToId));
        Assert.Equal(steps.Select(s => s.Score), rebuilt.Select(h => h.Score));
        // The hops chain: each hop starts where the previous one ended.
        for (var i = 1; i < rebuilt.Count; i++)
        {
            Assert.Equal(rebuilt[i - 1].ToId, rebuilt[i].FromId);
        }
    }

    [Fact]
    public void Taking_a_choice_with_several_targets_logs_one_hop_per_target_under_one_seq()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, "(:Team {name: 'Payments'})");
        var hop = f.Service.NextHop(open.CursorId, open.Position.Single().Id, "who is on the team");
        f.Service.Take(open.CursorId, Choice(hop, "MEMBER_OF", "in").ChoiceId);

        var rebuilt = f.Audit.Rebuild(open.CursorId);

        Assert.Equal(5, rebuilt.Count);
        Assert.All(rebuilt, h => Assert.Equal(1, h.Seq));
        Assert.Equal(5, rebuilt.Select(h => h.ToId).Distinct().Count());
    }

    [Fact]
    public void Rejected_takes_are_not_audited()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);

        Assert.Throws<CursorException>(() => f.Service.Take(open.CursorId, "c42"));

        Assert.Empty(f.Audit.Rebuild(open.CursorId));
    }

    [Fact]
    public void The_audit_graph_lives_apart_from_the_data()
    {
        using var f = new CursorFixture();
        WalkToTheTeam(f.Service);

        Assert.Equal(0, f.Data.Run("MATCH (n) WHERE n:Walk OR n:Hop RETURN count(n) AS c").Scalar<long>());
    }

    [Fact]
    public void The_audit_graph_persists_in_a_file_store_and_is_rebuilt_after_reopening()
    {
        var dir = Directory.CreateTempSubdirectory("graph-cursor-");
        var auditPath = Path.Combine(dir.FullName, "audit.rl");
        string cursorId;
        try
        {
            using (var data = RelateDatabase.Open())
            using (var audit = AuditLog.Open(auditPath))
            using (var stores = new StoreRegistry(new Dictionary<string, RelateDatabase> { ["default"] = data }))
            {
                Seed.SeedLoader.Load(data);
                var service = new CursorService(stores, new CursorOptions(), new TokenOverlapScorer(), audit, TimeProvider.System);
                (cursorId, _) = WalkToTheTeam(service);
            }

            using var reopened = RelateDatabase.OpenReadOnly(auditPath);
            var rows = reopened.Run(AuditLog.RebuildWalkCypher, new Dictionary<string, object?> { ["id"] = cursorId }).ToList();

            Assert.Equal(["AFFECTS", "DEPENDS_ON", "OWNED_BY"], rows.Select(r => r.Get<string>("relType")));
            Assert.Equal([1, 2, 3], rows.Select(r => r.Get<int>("seq")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
