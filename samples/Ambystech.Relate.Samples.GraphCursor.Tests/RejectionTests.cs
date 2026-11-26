using Ambystech.Relate.Samples.GraphCursor.Cursor;
using static Ambystech.Relate.Samples.GraphCursor.Tests.CursorFixture;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

/// <summary>Anything the cursor never offered is refused, and a refusal never moves the cursor.</summary>
public class RejectionTests
{
    private const string Goal = "find the owning team";

    [Theory]
    [InlineData("c999")]
    [InlineData("4:rl-id:n99999")]
    [InlineData("AFFECTS")]
    [InlineData("Dependency")]
    [InlineData("MATCH (n) RETURN n")]
    public void Take_rejects_a_target_the_cursor_never_returned(string invented)
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);
        f.Service.NextHop(open.CursorId, open.Position.Single().Id, Goal);

        var error = Assert.Throws<CursorException>(() => f.Service.Take(open.CursorId, invented));

        Assert.Equal("not_offered", error.Code);
        Assert.Equal(open.Position.Single().Id, f.Service.Walk(open.CursorId).Position.Single().Id);
        Assert.Empty(f.Service.Walk(open.CursorId).Steps);
    }

    [Fact]
    public void Take_rejects_a_real_node_the_cursor_never_showed()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);
        // A node that exists in the store but that this cursor has not been shown.
        var stranger = f.Data.Run("MATCH (t:Team {name: 'Growth'}) RETURN elementId(t) AS id").Scalar<string>()!;

        var error = Assert.Throws<CursorException>(() => f.Service.Take(open.CursorId, stranger));

        Assert.Equal("not_offered", error.Code);
        Assert.Empty(f.Service.Walk(open.CursorId).Steps);
    }

    [Fact]
    public void Take_rejects_a_node_that_was_shown_but_no_offered_choice_leads_to()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);
        var two = f.Service.Gather(open.CursorId, "neighborhood2");
        // Two hops away: shown by gather, but not adjacent to the cursor.
        var farAway = two.Nodes.First(n => n.Labels.Contains("Service")).Id;
        f.Service.NextHop(open.CursorId, open.Position.Single().Id, Goal);

        var error = Assert.Throws<CursorException>(() => f.Service.Take(open.CursorId, farAway));

        Assert.Equal("not_reachable", error.Code);
        Assert.Empty(f.Service.Walk(open.CursorId).Steps);
    }

    [Fact]
    public void Choices_expire_when_the_cursor_moves()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);
        var hop = f.Service.NextHop(open.CursorId, open.Position.Single().Id, Goal);
        var affects = Choice(hop, "AFFECTS", "out");
        var caused = Choice(hop, "CAUSED_BY", "in");
        f.Service.Take(open.CursorId, affects.ChoiceId);

        // The cursor moved, so the choice ids it offered from the old position are gone: unknown, not merely unreachable.
        var error = Assert.Throws<CursorException>(() => f.Service.Take(open.CursorId, caused.ChoiceId));

        Assert.Equal("not_offered", error.Code);
    }

    [Fact]
    public void Next_hop_only_expands_nodes_in_the_current_position()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);
        var elsewhere = f.Data.Run("MATCH (t:Team {name: 'Growth'}) RETURN elementId(t) AS id").Scalar<string>()!;

        var error = Assert.Throws<CursorException>(() => f.Service.NextHop(open.CursorId, elsewhere, Goal));

        Assert.Equal("not_in_position", error.Code);
    }

    [Fact]
    public void Unknown_cursor_and_missing_goal_are_clear_errors()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, PlantedCve);

        Assert.Equal("unknown_cursor", Assert.Throws<CursorException>(() => f.Service.Walk("nope")).Code);
        Assert.Equal("goal_required", Assert.Throws<CursorException>(() => f.Service.NextHop(open.CursorId, open.Position.Single().Id, " ")).Code);
    }

    [Fact]
    public void Open_only_accepts_configured_store_names_never_paths()
    {
        using var f = new CursorFixture();

        var error = Assert.Throws<CursorException>(() => f.Service.Open("C:/secrets/other.rl", PlantedCve));

        Assert.Equal("unknown_store", error.Code);
    }

    [Theory]
    [InlineData("MATCH (n) RETURN n")]
    [InlineData("(:CVE) DETACH DELETE n")]
    [InlineData("CALL db.labels()")]
    public void Raw_cypher_is_rejected_as_a_start_query_by_default(string cypher)
    {
        using var f = new CursorFixture();

        var error = Assert.Throws<CursorException>(() => f.Service.Open(null, cypher));

        Assert.Equal("start_query_rejected", error.Code);
    }

    [Fact]
    public void A_start_query_for_an_unknown_label_lists_the_real_ones()
    {
        using var f = new CursorFixture();

        var error = Assert.Throws<CursorException>(() => f.Service.Open(null, "(:Spaceship)"));

        Assert.Equal("unknown_label", error.Code);
        Assert.Contains("CVE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_query_is_disabled_by_default()
    {
        using var f = new CursorFixture();

        var error = Assert.Throws<CursorException>(() => f.Service.RunQuery(null, "MATCH (n) RETURN count(n) AS c"));

        Assert.Equal("raw_query_disabled", error.Code);
    }

    [Theory]
    [InlineData("CREATE (:Nope)")]
    [InlineData("MATCH (n:Team) DETACH DELETE n")]
    [InlineData("MATCH (n:Team) SET n.name = 'x'")]
    [InlineData("MERGE (:Nope {id: 1})")]
    public void Run_query_refuses_writes_when_enabled_and_changes_nothing(string write)
    {
        using var f = new CursorFixture(o => o.AllowRawQuery = true);
        var before = f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>();

        var error = Assert.Throws<CursorException>(() => f.Service.RunQuery(null, write));

        Assert.Equal("not_read_only", error.Code);
        Assert.Equal(before, f.Data.Run("MATCH (n) RETURN count(n) AS c").Scalar<long>());
        Assert.Equal(0, f.Data.Run("MATCH (n:Nope) RETURN count(n) AS c").Scalar<long>());
        Assert.Equal("Payments", f.Data.Run("MATCH (t:Team {id: 'team-payments'}) RETURN t.name AS n").Scalar<string>());
    }

    [Fact]
    public void Run_query_reads_with_a_row_cap_when_enabled()
    {
        using var f = new CursorFixture(o =>
        {
            o.AllowRawQuery = true;
            o.RowCap = 5;
        });

        var result = f.Service.RunQuery(null, "MATCH (p:Person) RETURN p.name AS name ORDER BY name");

        Assert.Equal(5, result.Rows.Count);
        Assert.True(result.Truncated);
        Assert.Equal(["name"], result.Columns);
    }

    [Fact]
    public void Raw_cypher_start_query_works_only_when_enabled_and_is_read_only()
    {
        using var f = new CursorFixture(o => o.AllowRawQuery = true);

        var open = f.Service.Open(null, "MATCH (c:CVE {id: 'CVE-2026-1234'}) RETURN c");
        Assert.Equal("CVE-2026-1234", open.Position.Single().Props["id"]);

        var error = Assert.Throws<CursorException>(() => f.Service.Open(null, "CREATE (c:CVE {id: 'x'}) RETURN c"));
        Assert.Equal("not_read_only", error.Code);
    }

    [Theory]
    [InlineData("(:CVE {id: 'CVE-2026-1234'})", "CVE", 1)]
    [InlineData(":Team", "Team", 0)]
    [InlineData("Service {tier: 'tier-1', language: 'csharp'}", "Service", 2)]
    [InlineData("(:Person {id: \"person-0\"})", "Person", 1)]
    public void Start_query_parser_accepts_the_restricted_form(string text, string label, int props)
    {
        Assert.True(StartQueryParser.TryParse(text, out var parsedLabel, out var where));

        Assert.Equal(label, parsedLabel);
        Assert.Equal(props, where.Count);
    }

    [Theory]
    [InlineData("MATCH (n) RETURN n")]
    [InlineData("(:CVE {id: 'a'}) RETURN 1")]
    [InlineData("(:CVE {id: 'a' + 'b'})")]
    [InlineData("(:CVE {id: $x})")]
    [InlineData("(:CVE)-[:AFFECTS]->(:Dependency)")]
    [InlineData("(:CVE {id: 'a\\'b'})")]
    [InlineData("")]
    public void Start_query_parser_rejects_everything_else(string text)
    {
        Assert.False(StartQueryParser.TryParse(text, out _, out _));
    }
}
