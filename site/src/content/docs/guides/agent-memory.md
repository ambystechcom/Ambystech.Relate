---
title: Agent memory
description: Vector search plus graph traversal in one Cypher statement.
---

`SEARCH` and graph traversal run in the same Cypher statement:

```csharp
db.Run("""
    CREATE VECTOR INDEX fact_embedding IF NOT EXISTS
    FOR (f:Fact) ON (f.embedding) WITH [f.closed]
    OPTIONS {indexConfig: {`vector.dimensions`: 1536, `vector.similarity_function`: 'cosine'}}
    """);

var recall = db.Run("""
    MATCH (f:Fact)
    SEARCH f IN (VECTOR INDEX fact_embedding FOR $q WHERE f.closed = false LIMIT 5) SCORE AS score
    MATCH (f)-[:FROM]->(ep:Episode)
    OPTIONAL MATCH (f)-[:SUPERSEDES]->(prev:Fact)
    RETURN f.text AS fact, score, ep.text AS evidence, prev.text AS replaced
    """, new Dictionary<string, object?> { ["q"] = embedding /* float[] */ });
```

New to vector indexes? Start with [Vector index](/guides/vector-index/).

A plain `float[]` parameter binds as a Cypher `LIST`; use `RelateVector` when you need a `VECTOR` value. See [Type mapping](/reference/type-mapping/).
