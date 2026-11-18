---
title: ADO.NET
description: Use Relate through DbConnection, DbCommand and DbDataReader.
---

```csharp
using Ambystech.Relate.Data;

using var conn = new RelateDbConnection("Data Source=./graph.rl");   // Mode=ReadOnly; Namespace=neo4j
conn.Open();

using var cmd = new RelateDbCommand("MATCH (p:Person) WHERE p.age >= $min RETURN p.name AS name", conn);
cmd.Parameters.AddWithValue("min", 30);          // "min", "$min" or "@min" all bind $min
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine(reader.GetString(0));
}
```

- Connections to the same store share one native database per process, because a store has a single writer. `:memory:` connections are private, as in SQLite.
- `ExecuteNonQuery` returns the graph changes (nodes and relationships created or deleted, plus properties set), or `-1`.
- Register the factory with `DbProviderFactories.RegisterFactory(RelateDbProviderFactory.InvariantName, RelateDbProviderFactory.Instance)`.
- **Dapper**: scalar parameters work (Dapper recognizes `$name`). Dapper expands `IEnumerable` parameters into `name1..nameN`, which Cypher's `$name` can't see, so pass lists and maps through `RelateDbCommand` or the driver API.
