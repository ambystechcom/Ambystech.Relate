using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class ValueMappingTests
{
    [Fact]
    public void Decodes_nodes_relationships_and_paths()
    {
        using var db = RelateDatabase.Open();
        db.Run(TestData.Team);

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
}
