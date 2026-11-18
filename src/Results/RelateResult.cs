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
