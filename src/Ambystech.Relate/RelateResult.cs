using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

/// <summary>
/// A statement's fully materialized result: column keys, records and summary. The native
/// result is copied and released before this object is returned, so it has no lifetime to manage.
/// </summary>
public sealed class RelateResult : IReadOnlyList<RelateRecord>
{
    internal RelateResult(IReadOnlyList<string> keys, IReadOnlyList<RelateRecord> records, RelateResultSummary summary)
    {
        Keys = keys;
        Records = records;
        Summary = summary;
    }

    /// <summary>The column names, in <c>RETURN</c> order.</summary>
    public IReadOnlyList<string> Keys { get; }

    /// <summary>The rows.</summary>
    public IReadOnlyList<RelateRecord> Records { get; }

    /// <summary>Counters, timings, notifications, plan and bookmark.</summary>
    public RelateResultSummary Summary { get; }

    /// <inheritdoc />
    public int Count => Records.Count;

    /// <inheritdoc />
    public RelateRecord this[int index] => Records[index];

    /// <summary>The only record; throws unless there is exactly one.</summary>
    public RelateRecord Single() => Records.Count == 1
        ? Records[0]
        : throw new InvalidOperationException($"Expected exactly one record but the result has {Records.Count}.");

    /// <summary>The only record's only (or first) column, converted.</summary>
    public T? Scalar<T>() => Single().Get<T>(0);

    /// <summary>Project every record.</summary>
    public IEnumerable<T> Select<T>(Func<RelateRecord, T> selector) => Records.Select(selector);

    /// <inheritdoc />
    public IEnumerator<RelateRecord> GetEnumerator() => Records.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static unsafe RelateResult Materialize(ResultHandle handle)
    {
        var r = handle.DangerousGetHandle();
        if (RelateNative.relate_result_is_error(r) != 0)
        {
            throw RelateException.FromErrorJson(NativeStrings.FromBorrowed(RelateNative.relate_result_error_json(r)), "Query");
        }

        var columns = checked((int)RelateNative.relate_column_count(r));
        var rows = checked((int)RelateNative.relate_row_count(r));

        var keys = new string[columns];
        var index = new Dictionary<string, int>(columns, StringComparer.Ordinal);
        for (var c = 0; c < columns; c++)
        {
            keys[c] = NativeStrings.FromBorrowed(RelateNative.relate_column_name(r, c)) ?? $"_{c}";
            index.TryAdd(keys[c], c);
        }

        var records = new RelateRecord[rows];
        for (var row = 0; row < rows; row++)
        {
            var values = new object?[columns];
            for (var c = 0; c < columns; c++)
            {
                values[c] = ValueReader.Read(RelateNative.relate_value_at(r, row, c));
            }

            records[row] = new RelateRecord(keys, index, values);
        }

        var summary = new RelateResultSummary(
            RelateResultSummary.ParseQueryType(NativeStrings.FromBorrowed(RelateNative.relate_result_query_type(r))),
            NativeStrings.FromBorrowed(RelateNative.relate_result_namespace(r)) ?? "neo4j",
            RelateCounters.Parse(NativeStrings.FromBorrowed(RelateNative.relate_result_stats(r))),
            RelateNotification.ParseAll(NativeStrings.FromBorrowed(RelateNative.relate_result_notifications(r))),
            NativeStrings.FromBorrowed(RelateNative.relate_result_plan_json(r)),
            NativeStrings.FromBorrowed(RelateNative.relate_result_bookmark(r)),
            RelateNative.relate_result_elapsed_ms(r),
            RelateNative.relate_result_parse_ms(r),
            RelateNative.relate_result_plan_ms(r),
            RelateNative.relate_result_execute_ms(r));

        return new RelateResult(keys, records, summary);
    }
}

/// <summary>One row, addressable by column name or position.</summary>
public sealed class RelateRecord : IReadOnlyDictionary<string, object?>
{
    private readonly IReadOnlyList<string> _keys;
    private readonly Dictionary<string, int> _index;
    private readonly object?[] _values;

    internal RelateRecord(IReadOnlyList<string> keys, Dictionary<string, int> index, object?[] values)
    {
        _keys = keys;
        _index = index;
        _values = values;
    }

    /// <summary>The column names.</summary>
    public IEnumerable<string> Keys => _keys;

    /// <summary>The values, in column order.</summary>
    public IEnumerable<object?> Values => _values;

    /// <inheritdoc />
    public int Count => _values.Length;

    /// <summary>The value of column <paramref name="key"/>.</summary>
    public object? this[string key] => _index.TryGetValue(key, out var i)
        ? _values[i]
        : throw new KeyNotFoundException($"The record has no column '{key}'. Columns: {string.Join(", ", _keys)}.");

    /// <summary>The value at column <paramref name="index"/>.</summary>
    public object? this[int index] => _values[index];

    /// <summary>The value of column <paramref name="key"/> converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(string key) => ValueConverter.Convert<T>(this[key]);

    /// <summary>The value at column <paramref name="index"/> converted to <typeparamref name="T"/>.</summary>
    public T? Get<T>(int index) => ValueConverter.Convert<T>(_values[index]);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _index.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        if (_index.TryGetValue(key, out var i))
        {
            value = _values[i];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>A copy as a dictionary.</summary>
    public Dictionary<string, object?> ToDictionary() =>
        _keys.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => _values[x.i], StringComparer.Ordinal);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < _values.Length; i++)
        {
            yield return new KeyValuePair<string, object?>(_keys[i], _values[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() =>
        "{" + string.Join(", ", this.Select(kv => $"{kv.Key}: {kv.Value}")) + "}";
}

/// <summary>The statement class, classified from the plan (ADR-0162).</summary>
public enum RelateQueryType
{
    /// <summary>Not reported.</summary>
    Unknown = 0,
    /// <summary><c>r</c>: reads only.</summary>
    Read,
    /// <summary><c>w</c>: writes only.</summary>
    Write,
    /// <summary><c>rw</c>: reads and writes.</summary>
    ReadWrite,
    /// <summary><c>s</c>: schema change (index, constraint).</summary>
    Schema,
}

/// <summary>What ran, how long it took and what it changed.</summary>
/// <param name="QueryType">The statement class.</param>
/// <param name="Namespace">The namespace the statement ran in.</param>
/// <param name="Counters">The 11 mutation counters.</param>
/// <param name="Notifications">Planner notifications (populated by EXPLAIN / PROFILE).</param>
/// <param name="PlanJson">The <c>relate.plan/v1</c> document for EXPLAIN / PROFILE, otherwise null.</param>
/// <param name="Bookmark">The auto-commit write's bookmark (<c>relate:&lt;n&gt;</c>), or null.</param>
/// <param name="ElapsedMilliseconds">Engine-reported total: parse, plan and execute.</param>
/// <param name="ParseMilliseconds">Parse and lowering window.</param>
/// <param name="PlanMilliseconds">Binding and planning window.</param>
/// <param name="ExecuteMilliseconds">Execution window.</param>
public sealed record RelateResultSummary(
    RelateQueryType QueryType,
    string Namespace,
    RelateCounters Counters,
    IReadOnlyList<RelateNotification> Notifications,
    string? PlanJson,
    string? Bookmark,
    double ElapsedMilliseconds,
    double ParseMilliseconds,
    double PlanMilliseconds,
    double ExecuteMilliseconds)
{
    internal static RelateQueryType ParseQueryType(string? value) => value switch
    {
        "r" => RelateQueryType.Read,
        "w" => RelateQueryType.Write,
        "rw" => RelateQueryType.ReadWrite,
        "s" => RelateQueryType.Schema,
        _ => RelateQueryType.Unknown,
    };
}

/// <summary>The canonical mutation counters (ADR-0216).</summary>
public sealed record RelateCounters(
    long NodesCreated,
    long NodesDeleted,
    long RelationshipsCreated,
    long RelationshipsDeleted,
    long PropertiesSet,
    long LabelsAdded,
    long LabelsRemoved,
    long IndexesAdded,
    long IndexesRemoved,
    long ConstraintsAdded,
    long ConstraintsRemoved)
{
    /// <summary>True when any counter is non-zero.</summary>
    public bool ContainsUpdates =>
        NodesCreated + NodesDeleted + RelationshipsCreated + RelationshipsDeleted + PropertiesSet + LabelsAdded + LabelsRemoved
        + IndexesAdded + IndexesRemoved + ConstraintsAdded + ConstraintsRemoved > 0;

    internal static readonly RelateCounters Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    internal static RelateCounters Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Empty;
        }

        using var doc = JsonDocument.Parse(json);
        var e = doc.RootElement;
        long Get(string name) => e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
        return new RelateCounters(
            Get("nodes_created"), Get("nodes_deleted"), Get("relationships_created"), Get("relationships_deleted"),
            Get("properties_set"), Get("labels_added"), Get("labels_removed"), Get("indexes_added"),
            Get("indexes_removed"), Get("constraints_added"), Get("constraints_removed"));
    }
}

/// <summary>A planner notification (ADR-0363), e.g. a Cartesian product warning.</summary>
public sealed record RelateNotification(
    string Code,
    string? GqlStatus,
    string Severity,
    string Category,
    string Title,
    string Description,
    string? Remedy,
    long? OperatorId)
{
    internal static IReadOnlyList<RelateNotification> ParseAll(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        var list = new List<RelateNotification>();
        foreach (var n in doc.RootElement.EnumerateArray())
        {
            string? S(string name) => n.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            list.Add(new RelateNotification(
                S("code") ?? "",
                S("gql_status"),
                S("severity") ?? "",
                S("category") ?? "",
                S("title") ?? "",
                S("description") ?? "",
                S("remedy"),
                n.TryGetProperty("operator_id", out var op) && op.TryGetInt64(out var id) ? id : null));
        }

        return list;
    }
}
