using System.ComponentModel;
using Ambystech.Relate.Samples.GraphCursor.Cursor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Ambystech.Relate.Samples.GraphCursor.Tools;

/// <summary>The expert escape hatch. Registered only when the server starts with <c>--allow-raw-query</c>.</summary>
[McpServerToolType]
public sealed class RawQueryTools(CursorService cursor)
{
    [McpServerTool(Name = "run_query", ReadOnly = true)]
    [Description("Run a read-only Cypher query. Writes are rejected; rows and time are capped. Prefer next_hop: it can only offer what exists.")]
    public CallToolResult RunQuery(
        [Description("A read-only Cypher query.")] string cypher,
        [Description("Which configured store to query. Omit when only one is configured.")] string? store = null) =>
        CursorTools.Call(() => cursor.RunQuery(store, cypher));
}
