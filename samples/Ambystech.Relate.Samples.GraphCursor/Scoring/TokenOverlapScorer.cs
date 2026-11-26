using System.Text;

namespace Ambystech.Relate.Samples.GraphCursor.Scoring;

/// <summary>
/// Deterministic, LLM-free scorer: token overlap between the goal and the relationship type (weighted
/// highest), the target labels and the sample property values, plus a small bonus for low fan-out.
/// </summary>
public sealed class TokenOverlapScorer : IHopScorer
{
    private const double RelTypeWeight = 0.5;
    private const double LabelWeight = 0.3;
    private const double ValueWeight = 0.2;
    private const double FanOutBonus = 0.05;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "an", "of", "to", "and", "or", "for", "in", "on", "by", "is", "are", "which", "what", "who",
        "find", "get", "that", "with", "from", "at", "it", "its", "this", "me", "all",
    };

    public double Score(string goal, HopCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var goalTokens = Tokenize(goal);

        var score =
            RelTypeWeight * Overlap(goalTokens, Tokenize(candidate.RelType)) +
            LabelWeight * Overlap(goalTokens, Tokenize(string.Join(' ', candidate.TargetLabels))) +
            ValueWeight * Overlap(goalTokens, Tokenize(string.Join(' ', candidate.SampleValues))) +
            FanOutBonus / (1 + Math.Log2(Math.Max(1, candidate.Count)));

        return Math.Round(Math.Min(1.0, score), 4);
    }

    /// <summary>
    /// Lowercase word stems: splits camelCase and any non-alphanumeric run, drops stop words and applies a
    /// deliberately crude suffix strip so "owns" and "OWNED_BY" meet at "own".
    /// </summary>
    internal static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();

        void Flush()
        {
            if (word.Length > 0)
            {
                var stem = Stem(word.ToString().ToLowerInvariant());
                if (stem.Length >= 2 && !StopWords.Contains(stem))
                {
                    tokens.Add(stem);
                }

                word.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!char.IsLetterOrDigit(c))
            {
                Flush();
                continue;
            }

            if (char.IsUpper(c) && i > 0 && char.IsLower(text[i - 1]))
            {
                Flush();
            }

            word.Append(c);
        }

        Flush();
        return tokens;
    }

    private static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal))
        {
            word = word[..^2];
        }
        else if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal))
        {
            word = word[..^3];
        }

        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
        {
            word = word[..^1];
        }

        if (word.Length > 3 && word.EndsWith('e'))
        {
            word = word[..^1];
        }

        return word;
    }

    private static double Overlap(HashSet<string> goal, HashSet<string> field)
    {
        if (goal.Count == 0 || field.Count == 0)
        {
            return 0;
        }

        var matches = field.Count(goal.Contains);
        return (double)matches / Math.Min(field.Count, goal.Count);
    }
}
