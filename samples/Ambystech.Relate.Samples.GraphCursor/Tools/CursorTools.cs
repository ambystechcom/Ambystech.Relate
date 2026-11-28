using System.ComponentModel;
using Ambystech.Relate.Samples.GraphCursor.Cursor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Ambystech.Relate.Samples.GraphCursor.Tools;

/// <summary>
/// The graph-walking tools. The agent never writes Cypher: it asks what is reachable from a node
/// (<c>next_hop</c>), picks one of the typed options and moves (<c>take</c>).
/// </summary>
[McpServerToolType]
public sealed class CursorTools(CursorService cursor)
{
    [McpServerTool(Name = "open", ReadOnly = true)]
    [Description(
        "Start a walk. Returns a cursorId, the start nodes, the relationship types reachable in one hop (with counts) " +
        "and the one-hop vocabulary (labels, relationship types, property keys). Everything you can later name comes from a response.")]
    public CallToolResult Open(
        [Description("Which configured store to walk. Omit when only one is configured.")] string? store = null,
        [Description("Where to start, as a single node pattern, e.g. (:CVE {id: 'CVE-2026-1234'}). Omit to start from the most connected nodes.")] string? startQuery = null) =>
        Call(() => cursor.Open(store, startQuery));

    [McpServerTool(Name = "next_hop", ReadOnly = true)]
    [Description(
        "List the typed choices from a node in the cursor's position: relationship type, direction, target labels, count, " +
        "up to 3 sample targets and a score against your goal. Pick one and call take with its choiceId.")]
    public CallToolResult NextHop(
        [Description("The cursor id returned by open.")] string cursorId,
        [Description("A node id from the cursor's current position.")] string nodeId,
        [Description("What you are trying to find, in plain words. Used only to rank the choices.")] string goal) =>
        Call(() => cursor.NextHop(cursorId, nodeId, goal));

    [McpServerTool(Name = "take")]
    [Description(
        "Move the cursor. Pass a choiceId from next_hop (moves to all of that choice's targets) or a node id the cursor " +
        "has shown you that an offered choice leads to. Anything else is rejected and the cursor does not move.")]
    public CallToolResult Take(
        [Description("The cursor id returned by open.")] string cursorId,
        [Description("A choiceId (e.g. c3) or a target node id from a previous response.")] string target) =>
        Call(() => cursor.Take(cursorId, target));

    [McpServerTool(Name = "gather", ReadOnly = true)]
    [Description(
        "A bounded local read around the cursor. scope is 'neighborhood2' (nodes within two hops), 'properties' " +
        "(all properties of the current nodes) or 'label:<L>' (nodes with a label you have already seen). Output is capped.")]
    public CallToolResult Gather(
        [Description("The cursor id returned by open.")] string cursorId,
        [Description("neighborhood2, properties or label:<L>.")] string scope) =>
        Call(() => cursor.Gather(cursorId, scope));

    [McpServerTool(Name = "walk", ReadOnly = true)]
    [Description("The walk so far: the start, every step taken, the current position and the evidence gathered. This is your context.")]
    public CallToolResult Walk([Description("The cursor id returned by open.")] string cursorId) =>
        Call(() => cursor.Walk(cursorId));

    /// <summary>
    /// Run a cursor operation. A refusal (<see cref="CursorException"/>) is an ordinary outcome for an agent that
    /// guessed wrong, so it is returned as a tool error rather than thrown: no stack trace, no server-side error log.
    /// </summary>
    internal static CallToolResult Call<T>(Func<T> action)
    {
        try
        {
            return new CallToolResult { Content = [new TextContentBlock { Text = Json.Serialize(action()) }] };
        }
        catch (CursorException e)
        {
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = e.Message }] };
        }
    }
}
