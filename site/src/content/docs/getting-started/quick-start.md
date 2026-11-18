---
title: Quick start
description: Open a database, write a graph and query it.
---

```csharp
using Ambystech.Relate;

using var db = RelateDatabase.Open("./team.rl");   // or RelateDatabase.Open() for :memory:

db.Run("""
    CREATE (ada:Person {name: 'Ada'}), (grace:Person {name: 'Grace'}),
           (relate:Project {name: 'relate'}),
           (ada)-[:WORKS_ON {since: 2024}]->(relate),
           (grace)-[:WORKS_ON {since: 2023}]->(relate)
    """);

var result = db.Run(
    "MATCH (p:Person)-[w:WORKS_ON]->(:Project {name: $project}) RETURN p.name AS name, w.since AS since ORDER BY since",
    new { project = "relate" });

foreach (var row in result)
{
    Console.WriteLine($"{row["name"]} since {row.Get<int>("since")}");
}

Console.WriteLine(result.Summary.Counters.NodesCreated);   // 0: it was a read
Console.WriteLine(result.Summary.ElapsedMilliseconds);     // engine-reported timing
```

`RelateDatabase.Run` uses a shared default session. For concurrent work, open a session per unit of work:

```csharp
using var session = db.OpenSession();
var top = await session.RunAsync("MATCH (p:Person) RETURN p ORDER BY p.name LIMIT 10");
RelateNode first = top[0].Get<RelateNode>("p")!;
```

## Next steps

- Group writes with [Transactions](/guides/transactions/).
- Combine vector search with traversal for [Agent memory](/guides/agent-memory/).
- Learn how values convert in [Type mapping](/reference/type-mapping/).
- See [Threading and store model](/reference/threading-and-store-model/) before sharing a database across threads.
