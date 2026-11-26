using Ambystech.Relate.Samples.GraphCursor.Scoring;
using static Ambystech.Relate.Samples.GraphCursor.Tests.CursorFixture;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

public class ScorerTests
{
    private static readonly TokenOverlapScorer Scorer = new();

    private static HopCandidate Hop(string relType, string[] labels, long count = 1, string[]? values = null, string direction = "out") =>
        new(relType, direction, labels, values ?? [], count);

    [Fact]
    public void A_matching_relationship_type_beats_a_matching_label_beats_a_matching_value()
    {
        const string goal = "which team owns it";

        var byRelType = Scorer.Score(goal, Hop("OWNED_BY", ["Thing"]));
        var byLabel = Scorer.Score(goal, Hop("RELATED_TO", ["Team"]));
        var byValue = Scorer.Score(goal, Hop("RELATED_TO", ["Thing"], values: ["team rocket"]));
        var nothing = Scorer.Score(goal, Hop("RELATED_TO", ["Thing"]));

        Assert.True(byRelType > byLabel, $"{byRelType} > {byLabel}");
        Assert.True(byLabel > byValue, $"{byLabel} > {byValue}");
        Assert.True(byValue > nothing, $"{byValue} > {nothing}");
    }

    [Fact]
    public void Word_forms_meet_in_the_middle()
    {
        // owns / OWNED_BY, services / Service, affects / AFFECTED
        Assert.True(Scorer.Score("who owns this", Hop("OWNED_BY", ["X"])) > Scorer.Score("who runs this", Hop("OWNED_BY", ["X"])));
        Assert.True(Scorer.Score("list services", Hop("X", ["Service"])) > Scorer.Score("list people", Hop("X", ["Service"])));
        Assert.True(Scorer.Score("what is affected", Hop("AFFECTS", ["X"])) > Scorer.Score("what is calling", Hop("AFFECTS", ["X"])));
    }

    [Fact]
    public void Camel_and_snake_case_are_split()
    {
        Assert.True(Scorer.Score("depends on libraries", Hop("DEPENDS_ON", ["Library"])) > 0.5);
        Assert.Equal(
            Scorer.Score("on call", Hop("ON_CALL_FOR", ["Person"])),
            Scorer.Score("on call", Hop("onCallFor", ["Person"])));
    }

    [Fact]
    public void Lower_fan_out_gets_a_small_bonus()
    {
        const string goal = "find the dependency";

        var narrow = Scorer.Score(goal, Hop("USES", ["Dependency"], count: 1));
        var wide = Scorer.Score(goal, Hop("USES", ["Dependency"], count: 500));

        Assert.True(narrow > wide);
        Assert.True(narrow - wide < 0.05, "fan-out only breaks ties; it must not outweigh a real match");
    }

    [Fact]
    public void Scores_are_deterministic_and_bounded()
    {
        var hop = Hop("OWNED_BY", ["Team"], values: ["Payments"]);

        var first = Scorer.Score("which team owns payments", hop);

        Assert.Equal(first, Scorer.Score("which team owns payments", hop));
        Assert.InRange(first, 0, 1);
        Assert.Equal(0.05, Scorer.Score("", Hop("X", ["Y"])), 4);
    }

    [Fact]
    public void Stop_words_alone_do_not_match()
    {
        Assert.Equal(
            Scorer.Score("the of and", Hop("BY", ["The"])),
            Scorer.Score("anything", Hop("BY", ["The"])));
    }

    [Fact]
    public void Next_hop_orders_choices_by_score_with_stable_ties()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, "(:Service {name: 'payments-gateway'})");
        var node = open.Position.Single().Id;

        var hop = f.Service.NextHop(open.CursorId, node, "which team owns this service");
        var again = f.Service.NextHop(open.CursorId, node, "which team owns this service");

        Assert.Equal(("OWNED_BY", "out"), (hop.Choices[0].RelType, hop.Choices[0].Direction));
        Assert.Equal(hop.Choices.Select(c => c.Score).OrderByDescending(s => s), hop.Choices.Select(c => c.Score));
        Assert.Equal(
            hop.Choices.Select(c => (c.RelType, c.Direction)),
            again.Choices.Select(c => (c.RelType, c.Direction)));
    }

    [Fact]
    public void A_different_goal_reorders_the_same_choices()
    {
        using var f = new CursorFixture();
        var open = f.Service.Open(null, "(:Service {name: 'payments-gateway'})");
        var node = open.Position.Single().Id;

        var owner = f.Service.NextHop(open.CursorId, node, "which team owns this service");
        var oncall = f.Service.NextHop(open.CursorId, node, "which person is on call");

        Assert.Equal("OWNED_BY", owner.Choices[0].RelType);
        Assert.Equal("ON_CALL_FOR", oncall.Choices[0].RelType);
    }

    [Fact]
    public void A_custom_scorer_can_replace_the_default()
    {
        using var f = new CursorFixture();
        var service = new GraphCursor.Cursor.CursorService(
            new GraphCursor.Cursor.StoreRegistry(new Dictionary<string, RelateDatabase> { ["default"] = f.Data }),
            f.Options,
            new AlphabeticalScorer(),
            f.Audit,
            TimeProvider.System);
        var open = service.Open(null, "(:Service {name: 'payments-gateway'})");

        var hop = service.NextHop(open.CursorId, open.Position.Single().Id, "which team owns this service");

        // The oracle ranks by relationship type, descending alphabetically, ignoring the goal entirely.
        Assert.Equal(hop.Choices.Select(c => c.RelType).OrderDescending(StringComparer.Ordinal), hop.Choices.Select(c => c.RelType));
    }

    private sealed class AlphabeticalScorer : IHopScorer
    {
        public double Score(string goal, HopCandidate candidate) => candidate.RelType[0] / 100.0 + candidate.RelType[1] / 10000.0;
    }
}
