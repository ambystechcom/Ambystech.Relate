using System.Text;

namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>One group of relationships leaving (or entering) a node: same type, same target labels.</summary>
internal sealed record HopGroup(string RelType, string Direction, IReadOnlyList<string> TargetLabels, long Count, IReadOnlyList<string> SampleIds);

/// <summary>
/// The only place that builds Cypher for the cursor. Every statement is parameterized and bounded with LIMIT,
/// so no tool can trigger an unbounded scan. Node ids are <c>elementId()</c> strings.
/// </summary>
internal sealed class GraphReader(CursorOptions options)
{
    private const int MaxGroups = 200;
    private const int MaxVocabularyKeys = 100;
    private static readonly string[] PreferredKeys = ["id", "name", "title", "severity", "tier", "version"];

    public static string Quote(string identifier) =>
        "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";

    public NodeView ToView(RelateNode node, bool allProperties = false)
    {
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        var source = allProperties
            ? node.Properties.Select(kv => kv)
            : PickKeyProperties(node.Properties);
        foreach (var (key, value) in source)
        {
            props[key] = Shorten(value, allProperties ? 200 : 80);
        }

        return new NodeView(node.ElementId, node.Labels, props);
    }

    public IReadOnlyList<NodeView> Nodes(RelateDatabase db, IReadOnlyCollection<string> ids, bool allProperties = false)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var result = db.Run(
            "MATCH (n) WHERE elementId(n) IN $ids RETURN n LIMIT $lim",
            Args(("ids", ids.ToList()), ("lim", ids.Count)));
        var byId = result.Select(r => r.Get<RelateNode>("n")!).ToDictionary(n => n.ElementId, StringComparer.Ordinal);
        return ids.Where(byId.ContainsKey).Select(id => ToView(byId[id], allProperties)).ToList();
    }

    public IReadOnlyList<string> AllLabels(RelateDatabase db) =>
        db.Run("CALL db.labels()").Select(r => r.Get<string>("label")!).ToList();

    public IReadOnlyList<NodeView> FindStart(RelateDatabase db, string label, IReadOnlyDictionary<string, object?> where)
    {
        var labels = AllLabels(db);
        if (!labels.Contains(label, StringComparer.Ordinal))
        {
            throw new CursorException("unknown_label", $"The store has no label '{label}'. Labels: {string.Join(", ", labels)}.");
        }

        var parameters = new Dictionary<string, object?> { ["lim"] = options.MaxNodes };
        var clauses = new List<string>();
        foreach (var (key, value) in where)
        {
            var name = $"p{clauses.Count}";
            clauses.Add($"n.{Quote(key)} = ${name}");
            parameters[name] = value;
        }

        var filter = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;
        var result = db.Run($"MATCH (n:{Quote(label)}){filter} RETURN n LIMIT $lim", parameters);
        return result.Select(r => ToView(r.Get<RelateNode>("n")!)).ToList();
    }

    /// <summary>With no start query: the most connected nodes.</summary>
    public IReadOnlyList<NodeView> DefaultStart(RelateDatabase db) =>
        db.Run(
                "MATCH (n)-[r]-() WITH n, count(r) AS degree RETURN n ORDER BY degree DESC LIMIT $lim",
                Args(("lim", options.MaxNodes)))
            .Select(r => ToView(r.Get<RelateNode>("n")!))
            .ToList();

    public IReadOnlyList<RelCount> RelCounts(RelateDatabase db, IReadOnlyCollection<string> ids, string direction)
    {
        var pattern = direction == "out" ? "(n)-[r]->(m)" : "(n)<-[r]-(m)";
        var result = db.Run(
            $"MATCH {pattern} WHERE elementId(n) IN $ids " +
            "WITH type(r) AS t, labels(m) AS l, count(*) AS c RETURN t, l, c ORDER BY c DESC, t LIMIT $lim",
            Args(("ids", ids.ToList()), ("lim", MaxGroups)));
        return result
            .Select(r => new RelCount(r.Get<string>("t")!, direction, Strings(r["l"]), r.Get<long>("c")))
            .ToList();
    }

    public Vocabulary BuildVocabulary(
        RelateDatabase db,
        IReadOnlyCollection<string> ids,
        IReadOnlyList<NodeView> nodes,
        IReadOnlyList<RelCount> outgoing,
        IReadOnlyList<RelCount> incoming)
    {
        var all = outgoing.Concat(incoming).ToList();
        var labels = nodes.SelectMany(n => n.Labels).Concat(all.SelectMany(c => c.TargetLabels))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var relTypes = all.Select(c => c.RelType).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var own = db.Run(
            "MATCH (n) WHERE elementId(n) IN $ids UNWIND keys(n) AS k RETURN DISTINCT k LIMIT $lim",
            Args(("ids", ids.ToList()), ("lim", MaxVocabularyKeys)));
        var neighbors = db.Run(
            "MATCH (n)--(m) WHERE elementId(n) IN $ids UNWIND keys(m) AS k RETURN DISTINCT k LIMIT $lim",
            Args(("ids", ids.ToList()), ("lim", MaxVocabularyKeys)));
        var keys = own.Concat(neighbors).Select(r => r.Get<string>("k")!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        return new Vocabulary(labels, relTypes, keys);
    }

    public IReadOnlyList<HopGroup> Groups(RelateDatabase db, string nodeId)
    {
        var groups = new List<HopGroup>();
        foreach (var direction in new[] { "out", "in" })
        {
            var pattern = direction == "out" ? "(n)-[r]->(m)" : "(n)<-[r]-(m)";
            var result = db.Run(
                $"MATCH {pattern} WHERE elementId(n) = $id " +
                "WITH type(r) AS t, labels(m) AS l, collect(elementId(m)) AS ids, count(*) AS c " +
                "RETURN t, l, c, ids[0..3] AS sample ORDER BY c DESC, t LIMIT $lim",
                Args(("id", nodeId), ("lim", MaxGroups)));
            groups.AddRange(result.Select(r =>
                new HopGroup(r.Get<string>("t")!, direction, Strings(r["l"]), r.Get<long>("c"), Strings(r["sample"]))));
        }

        return groups;
    }

    public IReadOnlyList<string> Targets(RelateDatabase db, OfferedChoice choice) =>
        db.Run(
                $"MATCH {Pattern(choice)} WHERE elementId(n) = $from AND {LabelFilter()} " +
                "RETURN DISTINCT elementId(m) AS id LIMIT $lim",
                Args(("from", choice.FromId), ("l", choice.TargetLabels.ToList()), ("lim", options.MaxNodes)))
            .Select(r => r.Get<string>("id")!)
            .ToList();

    public bool Adjacent(RelateDatabase db, OfferedChoice choice, string toId) =>
        db.Run(
                $"MATCH {Pattern(choice)} WHERE elementId(n) = $from AND elementId(m) = $to AND {LabelFilter()} " +
                "RETURN count(*) AS c",
                Args(("from", choice.FromId), ("to", toId), ("l", choice.TargetLabels.ToList())))
            .Scalar<long>() > 0;

    /// <summary>Nodes for a <c>gather</c> scope, bounded in Cypher. Fetches one more than the cap to detect truncation.</summary>
    public IReadOnlyList<NodeView> Gather(RelateDatabase db, string scope, IReadOnlyCollection<string> position)
    {
        var limit = options.MaxNodes + 1;
        switch (scope)
        {
            case "neighborhood2":
                return db.Run(
                        "MATCH (n)-[*1..2]-(m) WHERE elementId(n) IN $ids AND NOT elementId(m) IN $ids RETURN DISTINCT m LIMIT $lim",
                        Args(("ids", position.ToList()), ("lim", limit)))
                    .Select(r => ToView(r.Get<RelateNode>("m")!))
                    .ToList();
            case "properties":
                return db.Run(
                        "MATCH (n) WHERE elementId(n) IN $ids RETURN n LIMIT $lim",
                        Args(("ids", position.ToList()), ("lim", limit)))
                    .Select(r => ToView(r.Get<RelateNode>("n")!, allProperties: true))
                    .ToList();
            default:
                var label = scope["label:".Length..];
                return db.Run($"MATCH (n:{Quote(label)}) RETURN n LIMIT $lim", Args(("lim", limit)))
                    .Select(r => ToView(r.Get<RelateNode>("n")!))
                    .ToList();
        }
    }

    /// <summary>Apply the hard node and byte caps to gathered nodes.</summary>
    public (IReadOnlyList<NodeView> Nodes, bool Truncated, int Bytes) Cap(IReadOnlyList<NodeView> nodes)
    {
        var kept = new List<NodeView>();
        var bytes = 0;
        var truncated = false;
        foreach (var node in nodes)
        {
            var size = Encoding.UTF8.GetByteCount(Json.Serialize(node));
            if (kept.Count >= options.MaxNodes || bytes + size > options.MaxTextBytes)
            {
                truncated = true;
                break;
            }

            kept.Add(node);
            bytes += size;
        }

        return (kept, truncated, bytes);
    }

    internal static IReadOnlyList<string> Strings(object? value) =>
        value is IEnumerable<object?> list ? list.Select(x => x?.ToString() ?? string.Empty).ToList() : [];

    private static string Pattern(OfferedChoice choice) =>
        choice.Direction == "out"
            ? $"(n)-[r:{Quote(choice.RelType)}]->(m)"
            : $"(n)<-[r:{Quote(choice.RelType)}]-(m)";

    private static string LabelFilter() => "size(labels(m)) = size($l) AND ALL(x IN $l WHERE x IN labels(m))";

    private static IEnumerable<KeyValuePair<string, object?>> PickKeyProperties(IReadOnlyDictionary<string, object?> props) =>
        PreferredKeys.Where(props.ContainsKey)
            .Concat(props.Keys.Except(PreferredKeys, StringComparer.Ordinal))
            .Where(key => props[key] is string or long or double or bool)
            .Take(3)
            .Select(key => new KeyValuePair<string, object?>(key, props[key]));

    private static object? Shorten(object? value, int max) =>
        value switch
        {
            string s when s.Length > max => s[..max] + "…",
            string or long or double or bool or null => value,
            IEnumerable<object?> list => list.Select(x => Shorten(x, max)).ToList(),
            _ => Shorten(value.ToString(), max),
        };

    private static Dictionary<string, object?> Args(params (string Name, object? Value)[] values) =>
        values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
}
