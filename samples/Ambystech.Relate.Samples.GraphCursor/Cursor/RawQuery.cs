namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>
/// The expert escape hatch behind <c>run_query</c> and raw start queries. Read-only (checked with EXPLAIN, then run
/// in a transaction that is always rolled back), row-capped and wall-clock limited.
/// </summary>
/// <remarks>
/// The engine has no query timeout or cancellation, and results are materialized in full, so the timeout only
/// stops the caller from waiting (the statement may keep running) and the row cap truncates after execution.
/// </remarks>
internal static class RawQuery
{
    public static RelateResult RunReadOnly(RelateDatabase db, string cypher, CursorOptions options)
    {
        var text = cypher.Trim().TrimEnd(';');
        if (text.Length == 0)
        {
            throw new CursorException("invalid_cypher", "The query is empty.");
        }

        RelateResult plan;
        try
        {
            plan = db.Run("EXPLAIN " + text);
        }
        catch (RelateException e)
        {
            throw new CursorException("invalid_cypher", e.Message);
        }

        if (plan.Summary.QueryType != RelateQueryType.Read)
        {
            throw new CursorException("not_read_only", $"Only read queries are allowed; this one is '{plan.Summary.QueryType}'.");
        }

        var session = db.OpenSession();
        var task = Task.Run(() => session.ExecuteRead(tx => tx.Run(text)));
        _ = task.ContinueWith(_ => session.Dispose(), TaskScheduler.Default);

        try
        {
            if (!task.Wait(options.QueryTimeoutMilliseconds))
            {
                throw new CursorException(
                    "timeout",
                    $"The query did not finish within {options.QueryTimeoutMilliseconds} ms. The engine cannot cancel it, so it may still be running.");
            }

            return task.Result;
        }
        catch (AggregateException e) when (e.InnerException is RelateException inner)
        {
            throw new CursorException("query_failed", inner.Message);
        }
    }

    public static RawQueryResult Execute(RelateDatabase db, string cypher, CursorOptions options)
    {
        var result = RunReadOnly(db, cypher, options);
        var rows = result.Records
            .Take(options.RowCap)
            .Select(record => (IReadOnlyDictionary<string, object?>)record.ToDictionary()
                .ToDictionary(kv => kv.Key, kv => ToJson(kv.Value), StringComparer.Ordinal))
            .ToList();
        return new RawQueryResult(result.Keys, rows, result.Count > options.RowCap, result.Summary.ElapsedMilliseconds);
    }

    /// <summary>Every node in a result's columns, in order, up to <paramref name="max"/>.</summary>
    public static IReadOnlyList<RelateNode> Nodes(RelateResult result, int max) =>
        result.Records.SelectMany(r => r.Values).OfType<RelateNode>().Take(max).ToList();

    private static object? ToJson(object? value) =>
        value switch
        {
            RelateNode n => new { id = n.ElementId, labels = n.Labels, props = n.Properties },
            RelateRelationship r => new { id = r.ElementId, type = r.Type, start = r.StartElementId, end = r.EndElementId, props = r.Properties },
            RelatePath p => new { nodes = p.Nodes.Select(n => ToJson(n)).ToList(), relationships = p.Relationships.Select(r => ToJson(r)).ToList() },
            IEnumerable<object?> list => list.Select(ToJson).ToList(),
            IDictionary<string, object?> map => map.ToDictionary(kv => kv.Key, kv => ToJson(kv.Value), StringComparer.Ordinal),
            null or string or bool or int or long or double or float or decimal => value,
            _ => value.ToString(),
        };
}
