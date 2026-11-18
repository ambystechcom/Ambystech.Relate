using Ambystech.Relate;
using Ambystech.Relate.Data;

// Pass a path ending in .rl to persist the graph; defaults to a private in-memory store.
var uri = args.Length > 0 ? args[0] : RelateDatabase.InMemory;
Console.WriteLine($"Relate C ABI v{RelateDatabase.AbiVersion}, store: {uri}\n");

RunDriverDemo(uri);

// 6. The same store through ADO.NET. A store has one writer, so the driver database above is
// already closed; ADO.NET connections share one native database among themselves only.
Console.WriteLine("\n== ADO.NET");
if (uri == RelateDatabase.InMemory)
{
    Console.WriteLine("  (skipped: ':memory:' connections are private; pass a .rl path to see it)");
}
else
{
    using var conn = new RelateDbConnection($"Data Source={uri}");
    conn.Open();
    using var cmd = new RelateDbCommand("MATCH (p:Person) RETURN count(p) AS people", conn);
    Console.WriteLine($"  people in graph: {cmd.ExecuteScalar()}");
}

static void RunDriverDemo(string uri)
{
    using var db = RelateDatabase.Open(uri);

    // 1. Seed a small graph (idempotent, so re-running against a .rl file is safe).
    db.Run("""
        MERGE (ada:Person {name: 'Ada'})
        MERGE (grace:Person {name: 'Grace'})
        MERGE (linus:Person {name: 'Linus'})
        MERGE (relate:Project {name: 'relate'})
        MERGE (ada)-[:WORKS_ON {since: 2024}]->(relate)
        MERGE (grace)-[:WORKS_ON {since: 2023}]->(relate)
        MERGE (linus)-[:REVIEWS]->(relate)
        MERGE (ada)-[:KNOWS]->(grace)
        MERGE (grace)-[:KNOWS]->(linus)
        """);

    // 2. Parameterized query with typed access.
    Console.WriteLine("== Who works on 'relate'?");
    var workers = db.Run(
        "MATCH (p:Person)-[w:WORKS_ON]->(:Project {name: $project}) RETURN p.name AS name, w.since AS since ORDER BY since",
        new { project = "relate" });
    foreach (var row in workers)
    {
        Console.WriteLine($"  {row["name"]} since {row.Get<int>("since")}");
    }

    // 3. Rich values: nodes and paths.
    Console.WriteLine("\n== Shortest path Ada -> Linus");
    var path = db.Run("""
        MATCH p = shortestPath((:Person {name: 'Ada'})-[:KNOWS*]->(:Person {name: 'Linus'}))
        RETURN p
        """)[0].Get<RelatePath>("p")!;
    Console.WriteLine("  " + string.Join(" -> ", path.Nodes.Select(n => n.Properties["name"])));

    // 4. Explicit transaction: commit returns a bookmark; dispose without commit rolls back.
    Console.WriteLine("\n== Transactions");
    using var session = db.OpenSession();
    using (var tx = session.BeginTransaction())
    {
        tx.Run("CREATE (:Person {name: 'Temp'})");
    } // rolled back
    Console.WriteLine($"  after rollback: {Count(session, "Temp")} 'Temp' nodes");

    // ExecuteWrite commits for you and retries transient conflicts.
    session.ExecuteWrite(tx => tx.Run("CREATE (:Person {name: 'Margaret'})"));

    // Explicit commit returns a bookmark ("relate:<n>"); compare with RelateBookmark.Sequence.
    using (var tx = session.BeginTransaction())
    {
        tx.Run("CREATE (:Person {name: 'Barbara'})");
        Console.WriteLine($"  bookmark: {tx.Commit()}");
    }
    Console.WriteLine($"  after commit:   {Count(session, "Margaret")} 'Margaret' node(s)");

    // 5. Errors carry the engine's classification.
    Console.WriteLine("\n== Errors");
    try
    {
        db.Run("MATCH (n RETURN n");
    }
    catch (RelateException e)
    {
        Console.WriteLine($"  {e.GetType().Name}: stage={e.Stage}, code={e.Code}");
    }

}

static long Count(RelateSession s, string name) =>
    s.Run("MATCH (p:Person {name: $name}) RETURN count(p) AS c", new { name })[0].Get<long>("c");
