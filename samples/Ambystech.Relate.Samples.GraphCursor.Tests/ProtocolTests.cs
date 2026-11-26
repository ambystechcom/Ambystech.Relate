using static Ambystech.Relate.Samples.GraphCursor.Tests.CursorFixture;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

public class ProtocolTests
{
    private const string Goal = "find which team owns the service exposed to CVE-2026-1234";

    [Fact]
    public void Open_returns_the_start_node_the_relationship_types_and_the_one_hop_vocabulary()
    {
        using var f = new CursorFixture();

        var open = f.Service.Open(null, PlantedCve);

        var cve = Assert.Single(open.Position);
        Assert.Equal(["CVE"], cve.Labels);
        Assert.Equal("CVE-2026-1234", cve.Props["id"]);

        // CVE -[:AFFECTS]-> Dependency, and Incident -[:CAUSED_BY]-> CVE arrives from the other side.
        var affects = Assert.Single(open.Outgoing);
        Assert.Equal(("AFFECTS", "out", 1L), (affects.RelType, affects.Direction, affects.Count));
        Assert.Equal(["Dependency"], affects.TargetLabels);
        var causedBy = Assert.Single(open.Incoming);
        Assert.Equal(("CAUSED_BY", "in"), (causedBy.RelType, causedBy.Direction));

        Assert.Equal(["CVE", "Dependency", "Incident"], open.Vocabulary.Labels);
        Assert.Equal(["AFFECTS", "CAUSED_BY"], open.Vocabulary.RelTypes);
        Assert.Contains("severity", open.Vocabulary.PropertyKeys);
    }

    [Fact]
    public void Walks_from_the_cve_to_the_team_that_owns_the_exposed_service()
    {
        using var f = new CursorFixture();
        var s = f.Service;

        var open = s.Open(null, PlantedCve);
        var cve = open.Position.Single();

        // 1. CVE -> Dependency
        var hop1 = s.NextHop(open.CursorId, cve.Id, Goal);
        var affects = Choice(hop1, "AFFECTS", "out");
        Assert.Equal(["Dependency"], affects.TargetLabels);
        var atDependency = s.Take(open.CursorId, affects.ChoiceId);
        var dependency = atDependency.Position.Single();
        Assert.Equal("log-parse", dependency.Props["name"]);

        // 2. Dependency -> Service. The goal mentions the CVE, but retracing the step is ranked last.
        var hop2 = s.NextHop(open.CursorId, dependency.Id, Goal);
        Assert.Equal(("DEPENDS_ON", "in"), (hop2.Choices[0].RelType, hop2.Choices[0].Direction));
        Assert.True(Choice(hop2, "AFFECTS", "in").Backtrack);
        Assert.Equal("payments-gateway", Assert.Single(hop2.Choices[0].Samples).Props["name"]);
        var atService = s.Take(open.CursorId, hop2.Choices[0].ChoiceId);
        var service = atService.Position.Single();

        // 3. Service -> Team, the top-ranked choice for "which team owns".
        var hop3 = s.NextHop(open.CursorId, service.Id, Goal);
        Assert.Equal(("OWNED_BY", "out"), (hop3.Choices[0].RelType, hop3.Choices[0].Direction));
        var atTeam = s.Take(open.CursorId, hop3.Choices[0].ChoiceId);

        Assert.Equal("Payments", atTeam.Position.Single().Props["name"]);
        Assert.Equal(["Team"], atTeam.Position.Single().Labels);

        var walk = s.Walk(open.CursorId);
        Assert.Equal(Goal, walk.Goal);
        Assert.Equal(3, walk.Steps.Count);
        Assert.Equal(["AFFECTS", "DEPENDS_ON", "OWNED_BY"], walk.Steps.Select(x => x.RelType));
        Assert.Equal([1, 2, 3], walk.Steps.Select(x => x.Seq));
        Assert.Equal([cve.Id], walk.Start);
        Assert.Equal("Payments", walk.Position.Single().Props["name"]);
    }

    [Fact]
    public void Take_accepts_a_node_id_the_cursor_showed_and_an_offered_choice_leads_to()
    {
        using var f = new CursorFixture();
        var s = f.Service;
        var open = s.Open(null, PlantedCve);
        var hop = s.NextHop(open.CursorId, open.Position.Single().Id, Goal);
        var sample = Choice(hop, "AFFECTS", "out").Samples.Single();

        var moved = s.Take(open.CursorId, sample.Id);

        Assert.Equal(sample.Id, moved.Position.Single().Id);
        Assert.Equal("AFFECTS", moved.Step.RelType);
    }

    [Fact]
    public void Taking_a_choice_moves_to_its_whole_target_set()
    {
        using var f = new CursorFixture();
        var s = f.Service;
        var open = s.Open(null, "(:Team {name: 'Payments'})");
        var hop = s.NextHop(open.CursorId, open.Position.Single().Id, "who is on the team");

        var members = Choice(hop, "MEMBER_OF", "in");
        Assert.Equal(5, members.Count);
        Assert.Equal(3, members.Samples.Count);

        var moved = s.Take(open.CursorId, members.ChoiceId);

        Assert.Equal(5, moved.Position.Count);
        Assert.All(moved.Position, p => Assert.Equal(["Person"], p.Labels));
    }

    [Fact]
    public void Open_without_a_start_query_starts_from_the_most_connected_nodes()
    {
        using var f = new CursorFixture();

        var open = f.Service.Open(null, null);

        Assert.NotEmpty(open.Position);
        Assert.True(open.Position.Count <= f.Options.MaxNodes);
        Assert.NotEmpty(open.Outgoing.Concat(open.Incoming));
    }

    [Fact]
    public void Choices_are_capped()
    {
        using var f = new CursorFixture(o => o.MaxChoices = 2);
        var open = f.Service.Open(null, "(:Service {name: 'payments-gateway'})");

        var hop = f.Service.NextHop(open.CursorId, open.Position.Single().Id, Goal);

        Assert.Equal(2, hop.Choices.Count);
    }
}
