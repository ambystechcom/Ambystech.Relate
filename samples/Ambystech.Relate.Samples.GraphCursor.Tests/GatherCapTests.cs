using System.Text;
using Ambystech.Relate.Samples.GraphCursor.Cursor;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

public class GatherCapTests
{
    [Fact]
    public void Neighborhood_is_capped_by_node_count()
    {
        using var f = new CursorFixture(o =>
        {
            o.MaxNodes = 5;
            o.MaxTextBytes = 100_000;
        });
        // A team is two hops from dozens of nodes (members, services, repos, their dependencies...).
        var open = f.Service.Open(null, "(:Team {name: 'Payments'})");

        var gathered = f.Service.Gather(open.CursorId, "neighborhood2");

        Assert.Equal(5, gathered.Nodes.Count);
        Assert.True(gathered.Truncated);
    }

    [Fact]
    public void Output_is_capped_by_bytes_and_flagged()
    {
        using var f = new CursorFixture(o =>
        {
            o.MaxNodes = 50;
            o.MaxTextBytes = 400;
        });
        var open = f.Service.Open(null, "(:Team {name: 'Payments'})");

        var gathered = f.Service.Gather(open.CursorId, "neighborhood2");

        Assert.True(gathered.Truncated);
        Assert.True(gathered.Bytes <= 400);
        Assert.True(Encoding.UTF8.GetByteCount(Json.Serialize(gathered.Nodes)) <= 400 + gathered.Nodes.Count + 2);
        Assert.NotEmpty(gathered.Nodes);
    }

    [Fact]
    public void A_small_result_is_not_flagged()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, CursorFixture.PlantedCve);

        var gathered = f.Service.Gather(open.CursorId, "properties");

        var node = Assert.Single(gathered.Nodes);
        Assert.False(gathered.Truncated);
        Assert.Equal("critical", node.Props["severity"]);
        Assert.Equal("Remote code execution in log-parse", node.Props["summary"]);
    }

    [Fact]
    public void Label_scope_is_capped_and_limited_to_labels_the_cursor_has_seen()
    {
        using var f = new CursorFixture(o => o.MaxNodes = 7);
        var open = f.Service.Open(null, "(:Team {name: 'Payments'})");

        // 'Service' is in the vocabulary (OWNED_BY leads from services), so it has been seen.
        var services = f.Service.Gather(open.CursorId, "label:Service");
        Assert.Equal(7, services.Nodes.Count);
        Assert.True(services.Truncated);
        Assert.All(services.Nodes, n => Assert.Contains("Service", n.Labels));

        // 'CVE' exists in the store but is nowhere near this cursor.
        var error = Assert.Throws<CursorException>(() => f.Service.Gather(open.CursorId, "label:CVE"));
        Assert.Equal("unknown_label", error.Code);
    }

    [Theory]
    [InlineData("everything")]
    [InlineData("neighborhood3")]
    [InlineData("MATCH (n) RETURN n")]
    [InlineData("")]
    public void Unbounded_or_unknown_scopes_are_rejected(string scope)
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, CursorFixture.PlantedCve);

        var error = Assert.Throws<CursorException>(() => f.Service.Gather(open.CursorId, scope));

        Assert.Equal("bad_scope", error.Code);
    }

    [Fact]
    public void A_label_with_a_quote_cannot_inject_cypher()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, CursorFixture.PlantedCve);

        var error = Assert.Throws<CursorException>(() => f.Service.Gather(open.CursorId, "label:CVE`) DETACH DELETE (n"));

        Assert.Equal("unknown_label", error.Code);
        Assert.True(f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>() > 150);
    }

    [Fact]
    public void Gathered_evidence_shows_up_in_the_walk()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, CursorFixture.PlantedCve);
        f.Service.Gather(open.CursorId, "properties");
        f.Service.Gather(open.CursorId, "neighborhood2");

        var walk = f.Service.Walk(open.CursorId);

        Assert.Equal(["properties", "neighborhood2"], walk.Evidence.Select(e => e.Scope));
    }
}
