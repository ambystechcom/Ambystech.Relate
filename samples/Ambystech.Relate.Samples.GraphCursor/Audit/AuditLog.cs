using System.Globalization;
using Ambystech.Relate.Samples.GraphCursor.Cursor;

namespace Ambystech.Relate.Samples.GraphCursor.Audit;

/// <summary>One audited hop, as rebuilt from the audit graph.</summary>
public sealed record AuditHop(int Seq, string FromId, string RelType, string ToId, double Score);

/// <summary>
/// Judgment-Ledger-lite: every hop is logged as a graph in a store separate from the data,
/// <c>(:Walk {id, goal, startedAt})-[:STEP {seq, at, score}]->(:Hop {fromId, relType, toId})</c>.
/// </summary>
public sealed class AuditLog : IDisposable
{
    /// <summary>Rebuilds one walk, in order. Parameter: <c>$id</c>, the cursor id.</summary>
    public const string RebuildWalkCypher =
        "MATCH (w:Walk {id: $id})-[s:STEP]->(h:Hop) " +
        "RETURN s.seq AS seq, h.fromId AS fromId, h.relType AS relType, h.toId AS toId, s.score AS score " +
        "ORDER BY seq, toId";

    private readonly RelateSession _session;
    private readonly bool _ownsDatabase;

    public AuditLog(RelateDatabase database, bool ownsDatabase = false)
    {
        ArgumentNullException.ThrowIfNull(database);
        Database = database;
        _ownsDatabase = ownsDatabase;
        _session = database.OpenSession();
    }

    /// <summary>The audit store, for ad-hoc queries.</summary>
    public RelateDatabase Database { get; }

    /// <summary>Open the audit store: a <c>.rl</c> file, or a private in-memory store when null.</summary>
    public static AuditLog Open(string? path) =>
        new(RelateDatabase.Open(path ?? RelateDatabase.InMemory), ownsDatabase: true);

    public void StartWalk(string walkId, string store, DateTimeOffset at) =>
        _session.ExecuteWrite(tx => tx.Run(
            "MERGE (w:Walk {id: $id}) ON CREATE SET w.goal = '', w.store = $store, w.startedAt = $at",
            new Dictionary<string, object?> { ["id"] = walkId, ["store"] = store, ["at"] = Iso(at) }));

    public void SetGoal(string walkId, string goal) =>
        _session.ExecuteWrite(tx => tx.Run(
            "MATCH (w:Walk {id: $id}) SET w.goal = $goal",
            new Dictionary<string, object?> { ["id"] = walkId, ["goal"] = goal }));

    /// <summary>One <c>STEP</c> per target, all sharing the step's <c>seq</c>.</summary>
    public void RecordStep(string walkId, Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _session.ExecuteWrite(tx =>
        {
            foreach (var to in step.To)
            {
                tx.Run(
                    "MATCH (w:Walk {id: $id}) " +
                    "CREATE (w)-[:STEP {seq: $seq, at: $at, score: $score}]->(:Hop {fromId: $from, relType: $relType, toId: $to})",
                    new Dictionary<string, object?>
                    {
                        ["id"] = walkId,
                        ["seq"] = step.Seq,
                        ["at"] = Iso(step.At),
                        ["score"] = step.Score,
                        ["from"] = step.From,
                        ["relType"] = step.RelType,
                        ["to"] = to,
                    });
            }

            return 0;
        });
    }

    /// <summary>Rebuild a walk from the audit graph with <see cref="RebuildWalkCypher"/>.</summary>
    public IReadOnlyList<AuditHop> Rebuild(string walkId) =>
        _session.Run(RebuildWalkCypher, new Dictionary<string, object?> { ["id"] = walkId })
            .Select(r => new AuditHop(
                r.Get<int>("seq"),
                r.Get<string>("fromId")!,
                r.Get<string>("relType")!,
                r.Get<string>("toId")!,
                r.Get<double>("score")))
            .ToList();

    public void Dispose()
    {
        _session.Dispose();
        if (_ownsDatabase)
        {
            Database.Dispose();
        }
    }

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
