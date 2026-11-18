using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ambystech.Relate.Interop;

namespace Ambystech.Relate;

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
