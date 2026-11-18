using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class DriverTests
{
    private const string Team = """
        CREATE (ada:Person {name: 'Ada', role: 'engineer'}),
               (grace:Person {name: 'Grace', role: 'engineer'}),
               (noor:Person {name: 'Noor', role: 'maintainer'}),
               (relate:Project {name: 'relate', language: 'Rust'}),
               (tck:Project {name: 'opencypher-tck', language: 'Gherkin'}),
               (ada)-[:WORKS_ON {since: 2024}]->(relate),
               (grace)-[:WORKS_ON {since: 2023}]->(relate),
               (grace)-[:WORKS_ON {since: 2022}]->(tck),
               (noor)-[:MAINTAINS]->(relate),
               (ada)-[:KNOWS]->(grace)
        """;

    [Fact]
    public void Native_library_exports_the_whole_v2_abi()
    {
        Assert.Equal(2, RelateDatabase.AbiVersion);

        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", NativeLoader.RuntimeIdentifier(), "native", NativeLoader.FileName());
        var lib = System.Runtime.InteropServices.NativeLibrary.Load(Environment.GetEnvironmentVariable("RELATE_LIB") ?? path);
        var imported = typeof(RelateNative)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name.StartsWith("relate_", StringComparison.Ordinal))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.Equal(81, imported.Count);
        foreach (var name in imported)
        {
            Assert.True(System.Runtime.InteropServices.NativeLibrary.TryGetExport(lib, name, out _), $"missing export {name}");
        }
    }

    [Fact]
    public void Creates_and_reads_back_a_graph_with_counters()
    {
        using var db = RelateDatabase.Open();
        var write = db.Run(Team);

        Assert.Equal(RelateQueryType.Write, write.Summary.QueryType);
        Assert.Equal(5, write.Summary.Counters.NodesCreated);
        Assert.Equal(5, write.Summary.Counters.RelationshipsCreated);
        Assert.True(write.Summary.Counters.ContainsUpdates);
        Assert.NotNull(write.Summary.Bookmark);
        Assert.Equal("neo4j", write.Summary.Namespace);

        var read = db.Run("""
            MATCH (p:Person)-[w:WORKS_ON]->(:Project {name: $project})
            RETURN p.name AS name, w.since AS since ORDER BY since
            """, new Dictionary<string, object?> { ["project"] = "relate" });

        Assert.Equal(["name", "since"], read.Keys);
        Assert.Equal(2, read.Count);
        Assert.Equal("Grace", read[0]["name"]);
        Assert.Equal(2023L, read[0]["since"]);
        Assert.Equal(2024, read[1].Get<int>("since"));
        Assert.Equal(RelateQueryType.Read, read.Summary.QueryType);
    }

    [Fact]
    public void Anonymous_object_parameters_bind_by_property_name()
    {
        using var db = RelateDatabase.Open();
        db.Run(Team);

        var count = db.Run("MATCH (p:Person {role: $role}) RETURN count(p) AS n", new { role = "engineer" }).Scalar<long>();

        Assert.Equal(2, count);
    }

    [Fact]
    public void Decodes_nodes_relationships_and_paths()
    {
        using var db = RelateDatabase.Open();
        db.Run(Team);

        var row = db.Run("""
            MATCH p = (a:Person {name: 'Ada'})-[r:WORKS_ON]->(j:Project)
            RETURN a, r, j, p
            """).Single();

        var ada = row.Get<RelateNode>("a")!;
        var rel = row.Get<RelateRelationship>("r")!;
        var project = row.Get<RelateNode>("j")!;
        var path = row.Get<RelatePath>("p")!;

        Assert.True(ada.HasLabel("Person"));
        Assert.Equal("Ada", ada.Get<string>("name"));
        Assert.False(string.IsNullOrEmpty(ada.ElementId));
        Assert.Equal("WORKS_ON", rel.Type);
        Assert.Equal(2024L, rel.Properties["since"]);
        Assert.Equal(ada.ElementId, rel.StartElementId);
        Assert.Equal(project.ElementId, rel.EndElementId);
        Assert.Equal(1, path.Length);
        Assert.Equal("Ada", path.Start.Properties["name"]);
        Assert.Equal("relate", path.End.Properties["name"]);
    }

    [Fact]
    public void Round_trips_scalars_collections_and_maps()
    {
        using var db = RelateDatabase.Open();
        var parameters = new Dictionary<string, object?>
        {
            ["b"] = true,
            ["i"] = int.MaxValue,
            ["l"] = long.MinValue,
            ["f"] = 1.5,
            ["s"] = "hola, ñandú 🦎",
            ["nul"] = null,
            ["list"] = new object?[] { 1, "two", null, 3.0 },
            ["map"] = new Dictionary<string, object?> { ["k"] = "v", ["nested"] = new[] { 1, 2 } },
            ["bytes"] = new byte[] { 0, 127, 255 },
        };

        var row = db.Run("RETURN $b AS b, $i AS i, $l AS l, $f AS f, $s AS s, $nul AS nul, $list AS list, $map AS map, $bytes AS bytes", parameters).Single();

        Assert.Equal(true, row["b"]);
        Assert.Equal((long)int.MaxValue, row["i"]);
        Assert.Equal(long.MinValue, row["l"]);
        Assert.Equal(1.5, row["f"]);
        Assert.Equal("hola, ñandú 🦎", row["s"]);
        Assert.Null(row["nul"]);
        Assert.Equal(new object?[] { 1L, "two", null, 3.0 }, (List<object?>)row["list"]!);
        var map = (Dictionary<string, object?>)row["map"]!;
        Assert.Equal("v", map["k"]);
        Assert.Equal(new object?[] { 1L, 2L }, (List<object?>)map["nested"]!);
        Assert.Equal(new byte[] { 0, 127, 255 }, row.Get<byte[]>("bytes"));
    }

    [Fact]
    public void Round_trips_temporals_durations_and_points()
    {
        using var db = RelateDatabase.Open();
        var dto = new DateTimeOffset(2026, 11, 16, 18, 30, 15, 123, TimeSpan.FromHours(-6));
        var parameters = new Dictionary<string, object?>
        {
            ["date"] = new DateOnly(2026, 11, 16),
            ["ltime"] = new TimeOnly(18, 30, 15, 250),
            ["ldt"] = new DateTime(2026, 11, 16, 18, 30, 15, DateTimeKind.Unspecified),
            ["dto"] = dto,
            ["span"] = TimeSpan.FromMinutes(90),
            ["months"] = new RelateDuration(14, 3, 10, 5),
            ["pt"] = RelatePoint.Wgs84(-100.3161, 25.6866),
            ["pt3"] = RelatePoint.Cartesian(1, 2, 3),
            ["zoned"] = RelateTemporal.ZonedDateTime(new DateTime(2026, 7, 1, 12, 0, 0), "America/Monterrey"),
            ["otime"] = RelateTemporal.OffsetTime(new TimeOnly(9, 15), TimeSpan.FromHours(2)),
        };

        var row = db.Run("""
            RETURN $date AS date, $ltime AS ltime, $ldt AS ldt, $dto AS dto, $span AS span, $months AS months,
                   $pt AS pt, $pt3 AS pt3, $zoned AS zoned, $otime AS otime,
                   date('2026-11-16') + duration('P1D') AS tomorrow
            """, parameters).Single();

        Assert.Equal(new DateOnly(2026, 11, 16), row["date"]);
        Assert.Equal(new TimeOnly(18, 30, 15, 250), row["ltime"]);
        Assert.Equal(new DateTime(2026, 11, 16, 18, 30, 15), row["ldt"]);
        Assert.Equal(dto, row["dto"]);
        Assert.Equal(TimeSpan.FromMinutes(90), row.Get<TimeSpan>("span"));
        Assert.Equal(new RelateDuration(14, 3, 10, 5), row["months"]);
        Assert.Equal(RelatePoint.Wgs84(-100.3161, 25.6866), row["pt"]);
        Assert.Equal(RelatePoint.Cartesian(1, 2, 3), row["pt3"]);
        Assert.Equal(new DateOnly(2026, 11, 17), row["tomorrow"]);

        var zoned = Assert.IsType<RelateTemporal>(row["zoned"]);
        Assert.Equal("America/Monterrey", zoned.Zone);
        Assert.Equal(-6 * 3600, zoned.OffsetSeconds);

        var otime = Assert.IsType<RelateTemporal>(row["otime"]);
        Assert.Equal(RelateTemporalKind.Time, otime.Kind);
        Assert.Equal(9, otime.Hour);
        Assert.Equal(7200, otime.OffsetSeconds);
    }

    [Fact]
    public void Sub_100ns_precision_is_kept_losslessly()
    {
        using var db = RelateDatabase.Open();
        var value = db.Run("RETURN localtime('12:00:00.123456789') AS t").Scalar<object>();

        var t = Assert.IsType<RelateTemporal>(value);
        Assert.Equal(123456789, t.Nanosecond);
    }

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

    [Fact]
    public void Typed_vectors_round_trip()
    {
        using var db = RelateDatabase.Open();
        var row = db.Run("RETURN $v AS v, $iv AS iv", new Dictionary<string, object?>
        {
            ["v"] = RelateVector.Float32([0.5f, -1.25f, 2f]),
            ["iv"] = RelateVector.Integer([1, 2, 3], "INTEGER8"),
        }).Single();

        var v = Assert.IsType<RelateVector>(row["v"]);
        Assert.Equal("FLOAT32", v.Dtype);
        Assert.Equal(new[] { 0.5f, -1.25f, 2f }, row.Get<float[]>("v"));

        var iv = Assert.IsType<RelateVector>(row["iv"]);
        Assert.Equal("INTEGER8", iv.Dtype);
        Assert.Equal(new long[] { 1, 2, 3 }, iv.Integers!.ToArray());
    }

    [Fact]
    public void Transactions_commit_with_bookmark_and_roll_back()
    {
        using var db = RelateDatabase.Open();
        using var session = db.OpenSession();

        string? bookmark;
        using (var tx = session.BeginTransaction())
        {
            tx.Run("CREATE (:Item {n: 1})");
            Assert.True(session.InTransaction);
            bookmark = tx.Commit();
        }

        Assert.NotNull(bookmark);
        Assert.True(RelateBookmark.Sequence(bookmark!) > 0);

        using (var tx = session.BeginTransaction())
        {
            tx.Run("CREATE (:Item {n: 2})");
            // disposed without commit -> rollback
        }

        Assert.False(session.InTransaction);
        Assert.Equal(1L, session.Run("MATCH (i:Item) RETURN count(i) AS n").Scalar<long>());

        using (var tx = session.BeginTransaction())
        {
            tx.Run("MATCH (i:Item) RETURN i");
            Assert.Null(tx.Commit()); // a read-only transaction commits nothing
        }
    }

    [Fact]
    public void ExecuteWrite_commits_and_returns_the_work_value()
    {
        using var db = RelateDatabase.Open();
        using var session = db.OpenSession();

        var created = session.ExecuteWrite(tx =>
            tx.Run("UNWIND range(1, $n) AS i CREATE (:Item {i: i})", new { n = 3 }).Summary.Counters.NodesCreated);

        Assert.Equal(3, created);
        Assert.Equal(3L, session.ExecuteRead(tx => tx.Run("MATCH (i:Item) RETURN count(i)").Scalar<long>()));
    }

    [Fact]
    public void Statement_errors_are_classified()
    {
        using var db = RelateDatabase.Open();

        var syntax = Assert.Throws<RelateClientException>(() => db.Run("MATCH (n RETURN n"));
        Assert.StartsWith("Neo.ClientError.", syntax.Code);
        Assert.Equal(RelateErrorStage.Compile, syntax.Stage);
        Assert.False(syntax.Retryable);
        Assert.False(string.IsNullOrEmpty(syntax.GqlStatus));

        var missing = Assert.Throws<RelateClientException>(() => db.Run("RETURN $nope AS x"));
        Assert.Contains("nope", missing.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void File_store_persists_and_reopens_read_only()
    {
        var dir = Directory.CreateTempSubdirectory("relate-");
        var file = Path.Combine(dir.FullName, "graph.rl");
        try
        {
            using (var db = RelateDatabase.Open(file))
            {
                db.Run(Team);
            }

            using (var ro = RelateDatabase.OpenReadOnly(file))
            {
                Assert.True(ro.IsReadOnly);
                Assert.Equal(5L, ro.Run("MATCH (n) RETURN count(n) AS n").Scalar<long>());
                Assert.ThrowsAny<RelateException>(() => ro.Run("CREATE (:Nope)"));
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Second_writer_on_the_same_file_is_refused()
    {
        var dir = Directory.CreateTempSubdirectory("relate-");
        var file = Path.Combine(dir.FullName, "graph.rl");
        try
        {
            using var first = RelateDatabase.Open(file);
            first.Run("CREATE (:A)");
            Assert.ThrowsAny<RelateException>(() => RelateDatabase.Open(file));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Explain_returns_a_plan_document()
    {
        using var db = RelateDatabase.Open();
        var result = db.Run("EXPLAIN MATCH (a:Person), (b:Project) RETURN a, b");

        Assert.NotNull(result.Summary.PlanJson);
        Assert.Contains("relate.plan/v1", result.Summary.PlanJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_survives_database_dispose_until_closed()
    {
        var db = RelateDatabase.Open();
        var session = db.OpenSession();
        db.Dispose();

        // The native store is held open by the session's reference.
        Assert.Equal(1L, session.Run("RETURN 1 AS one").Scalar<long>());
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Run("RETURN 1"));
    }

    [Fact]
    public async Task Sessions_run_in_parallel()
    {
        using var db = RelateDatabase.Open();
        db.Run("UNWIND range(1, 100) AS i CREATE (:N {i: i})");

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var session = db.OpenSession();
            var r = await session.RunAsync("MATCH (n:N) RETURN sum(n.i) AS s");
            return r.Scalar<long>();
        });

        var sums = await Task.WhenAll(tasks);
        Assert.All(sums, s => Assert.Equal(5050L, s));
    }
}
