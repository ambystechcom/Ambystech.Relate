using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class VectorSearchTests
{
    [Fact]
    public void Vector_index_search_and_traversal_in_one_statement()
    {
        using var db = RelateDatabase.Open();
        db.Run("""
            CREATE VECTOR INDEX fact_embedding IF NOT EXISTS
            FOR (f:Fact) ON (f.embedding) WITH [f.closed]
            OPTIONS {indexConfig: {`vector.dimensions`: 4, `vector.similarity_function`: 'cosine'}}
            """);
        db.Run("""
            CREATE (api:Service {name: 'billing-api'}), (web:Service {name: 'web-app'}), (web)-[:CALLS]->(api),
                   (:Fact {text: 'billing-api times out', embedding: [0.9, 0.1, 0.0, 0.1], closed: true})-[:ABOUT]->(api),
                   (:Fact {text: 'billing-api fixed', embedding: [0.85, 0.15, 0.05, 0.1], closed: false})-[:ABOUT]->(api),
                   (:Fact {text: 'redis uses lru', embedding: [0.1, 0.9, 0.1, 0.0], closed: false})
            """);

        var result = db.Run("""
            MATCH (f:Fact)
            SEARCH f IN (VECTOR INDEX fact_embedding FOR $q WHERE f.closed = false LIMIT 1) SCORE AS score
            MATCH (f)-[:ABOUT]->(svc)<-[:CALLS]-(caller)
            RETURN f.text AS fact, score, caller.name AS caller
            """, new Dictionary<string, object?> { ["q"] = new[] { 0.88, 0.12, 0.02, 0.1 } });

        var row = result.Single();
        Assert.Equal("billing-api fixed", row["fact"]);
        Assert.Equal("web-app", row["caller"]);
        Assert.True(row.Get<double>("score") > 0.99);
    }
}
