---
title: Vector index
description: Create a vector index, store embeddings and run similarity search with SEARCH.
---

A vector index lets you find the nodes whose embeddings are closest to a query vector. In Relate it is plain Cypher: you create the index with `CREATE VECTOR INDEX` and query it with `SEARCH`, so everything below works through `db.Run(...)` or a session.

## 1. Create the index

```csharp
db.Run("""
    CREATE VECTOR INDEX fact_embedding IF NOT EXISTS
    FOR (f:Fact) ON (f.embedding) WITH [f.closed]
    OPTIONS {indexConfig: {`vector.dimensions`: 1536, `vector.similarity_function`: 'cosine'}}
    """);
```

| Part | Meaning |
| --- | --- |
| `fact_embedding` | The index name you pass to `SEARCH` later. |
| `FOR (f:Fact) ON (f.embedding)` | Index the `embedding` property of nodes labeled `Fact`. |
| `WITH [f.closed]` | Extra properties stored with the index so you can filter on them inside `SEARCH ... WHERE`. Optional. |
| `vector.dimensions` | The number of coordinates every embedding must have. Match your embedding model (for example `1536`). |
| `vector.similarity_function` | How closeness is measured. The examples here use `'cosine'`. |

`IF NOT EXISTS` makes the statement safe to run on every startup.

## 2. Store embeddings

Write the embedding as a list of numbers on the indexed property. Parameters keep large vectors out of the query text; a plain `float[]` or `double[]` binds as a Cypher `LIST`:

```csharp
float[] embedding = await embedder.EmbedAsync("billing-api times out");

db.Run(
    "CREATE (:Fact {text: $text, embedding: $embedding, closed: $closed})",
    new Dictionary<string, object?>
    {
        ["text"] = "billing-api times out",
        ["embedding"] = embedding,
        ["closed"] = false,
    });
```

`embedder` stands for whatever produces your embeddings (an API client, a local model); Relate only stores and searches the vectors.

## 3. Search

```csharp
var hits = db.Run("""
    MATCH (f:Fact)
    SEARCH f IN (VECTOR INDEX fact_embedding FOR $q LIMIT 5) SCORE AS score
    RETURN f.text AS fact, score
    """, new Dictionary<string, object?> { ["q"] = queryEmbedding });

foreach (var row in hits)
{
    Console.WriteLine($"{row.Get<double>("score"):F3}  {row["fact"]}");
}
```

- `MATCH (f:Fact)` declares the variable that `SEARCH` binds to the matching nodes.
- `FOR $q` is the query vector, which must have the same number of dimensions as the index.
- `LIMIT 5` is how many nearest neighbors to return.
- `SCORE AS score` returns the similarity as a `double`. With `cosine`, near-identical vectors score close to `1`.

### Filter while searching

Properties listed in `WITH [...]` when the index was created can be used in a `WHERE` inside `SEARCH`, so the filter applies to the nearest-neighbor search itself instead of trimming results afterward:

```csharp
var open = db.Run("""
    MATCH (f:Fact)
    SEARCH f IN (VECTOR INDEX fact_embedding FOR $q WHERE f.closed = false LIMIT 5) SCORE AS score
    RETURN f.text AS fact, score
    """, new Dictionary<string, object?> { ["q"] = queryEmbedding });
```

## 4. Combine with graph traversal

Because `SEARCH` is part of the query, you can keep matching from the nodes it finds. This returns the closest open fact and the service that calls the service it is about:

```csharp
var row = db.Run("""
    MATCH (f:Fact)
    SEARCH f IN (VECTOR INDEX fact_embedding FOR $q WHERE f.closed = false LIMIT 1) SCORE AS score
    MATCH (f)-[:ABOUT]->(svc)<-[:CALLS]-(caller)
    RETURN f.text AS fact, score, caller.name AS caller
    """, new Dictionary<string, object?> { ["q"] = queryEmbedding }).Single();
```

See [Agent memory](/guides/agent-memory/) for a fuller example that follows `FROM` and `SUPERSEDES` relationships.

## Typed vectors

When you need to pick the vector's element type explicitly, use `RelateVector` instead of a plain array:

```csharp
var v = RelateVector.Float32([0.5f, -1.25f, 2f]);   // FLOAT32, the usual embedding dtype
var d = RelateVector.Float([0.5, 1.5], "FLOAT");    // FLOAT, FLOAT16, BFLOAT16, FLOAT32
var i = RelateVector.Integer([1, 2, 3], "INTEGER8"); // INTEGER, INTEGER8, INTEGER16, INTEGER32
```

Vector values come back from queries as `RelateVector`. Convert them with `row.Get<float[]>("v")`, `Get<double[]>` for float vectors or `Get<long[]>` for integer vectors, and check `Dtype` and `Dimensions` on the value itself. See [Type mapping](/reference/type-mapping/).

## Things to keep in mind

- Embeddings must match `vector.dimensions`. Use one index per embedding model, since different models produce different sizes.
- The whole graph, indexes included, lives in memory. See [Threading and store model](/reference/threading-and-store-model/).
- Relate's engine decides which `vector.similarity_function` values and index options are available. Check [Relate's documentation](https://relatedb.ai) for the full list.
