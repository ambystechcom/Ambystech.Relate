using System.Reflection;

namespace Ambystech.Relate.Samples.GraphCursor.Seed;

/// <summary>Loads the demo "software org" graph. Idempotent: a marker node makes a second load a no-op.</summary>
public static class SeedLoader
{
    private const string ResourceName = "org.cypher";

    /// <summary>The seed script split into single statements (the engine runs one statement per call).</summary>
    public static IReadOnlyList<string> Statements()
    {
        using var stream = typeof(SeedLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        var script = string.Join(
            '\n',
            reader.ReadToEnd().Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        return script.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    /// <summary>Load the dataset; returns false when it was already there.</summary>
    public static bool Load(RelateDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        var seeded = db.Run("MATCH (m:Meta {key: 'seed'}) RETURN count(m) AS c").Scalar<long>() > 0;
        if (seeded)
        {
            return false;
        }

        using var session = db.OpenSession();
        session.ExecuteWrite(tx =>
        {
            foreach (var statement in Statements())
            {
                tx.Run(statement);
            }

            return 0;
        });
        return true;
    }
}
