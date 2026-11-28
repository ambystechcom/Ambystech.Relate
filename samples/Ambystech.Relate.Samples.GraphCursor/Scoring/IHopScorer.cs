namespace Ambystech.Relate.Samples.GraphCursor.Scoring;

/// <summary>What the scorer knows about one hop option.</summary>
/// <param name="RelType">The relationship type.</param>
/// <param name="Direction"><c>out</c> or <c>in</c>, relative to the node being expanded.</param>
/// <param name="TargetLabels">Labels of the nodes the hop leads to.</param>
/// <param name="SampleValues">String property values of the sample targets.</param>
/// <param name="Count">How many relationships of this kind leave the node (the fan-out).</param>
public sealed record HopCandidate(
    string RelType,
    string Direction,
    IReadOnlyList<string> TargetLabels,
    IReadOnlyList<string> SampleValues,
    long Count);

/// <summary>
/// Ranks hop options against a goal. The agent makes the decision; the scorer only orders the options.
/// </summary>
/// <remarks>
/// This is the extension point for a calibrated decision-oracle scorer (not implemented here): register
/// a different <see cref="IHopScorer"/> in DI and the cursor uses it unchanged.
/// </remarks>
public interface IHopScorer
{
    /// <summary>A score where higher means a better match for <paramref name="goal"/>.</summary>
    double Score(string goal, HopCandidate candidate);
}
