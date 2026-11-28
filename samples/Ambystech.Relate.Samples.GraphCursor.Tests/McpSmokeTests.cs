using System.Text.Json;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

/// <summary>The real server over real stdio: the protocol works end to end and stdout carries nothing else.</summary>
public class McpSmokeTests
{
    private static List<string> ToolNames(JsonElement listResponse) =>
        listResponse.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToList();

    [Fact]
    public async Task Lists_the_five_cursor_tools_and_hides_run_query_by_default()
    {
        using var server = new StdioServer("--seed");
        await server.InitializeAsync();

        var tools = ToolNames(await server.RequestAsync("tools/list"));

        Assert.Equal(["gather", "next_hop", "open", "take", "walk"], tools);
        Assert.Contains("run_query disabled", server.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_query_is_listed_only_with_the_flag()
    {
        using var server = new StdioServer("--seed", "--allow-raw-query");
        await server.InitializeAsync();

        var tools = ToolNames(await server.RequestAsync("tools/list"));

        Assert.Equal(["gather", "next_hop", "open", "run_query", "take", "walk"], tools);
        Assert.Contains("run_query ENABLED", server.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_walk_over_stdio_and_stdout_stays_protocol_only()
    {
        using var server = new StdioServer("--seed");
        await server.InitializeAsync();

        var (openText, openError) = await server.CallToolAsync("open", new { startQuery = "(:CVE {id: 'CVE-2026-1234'})" });
        Assert.False(openError, openText);
        using var open = JsonDocument.Parse(openText);
        var cursorId = open.RootElement.GetProperty("cursorId").GetString()!;
        var cveId = open.RootElement.GetProperty("position")[0].GetProperty("id").GetString()!;

        var (hopText, hopError) = await server.CallToolAsync("next_hop", new { cursorId, nodeId = cveId, goal = "find which team owns the service exposed to this CVE" });
        Assert.False(hopError, hopText);
        using var hop = JsonDocument.Parse(hopText);
        var affects = hop.RootElement.GetProperty("choices").EnumerateArray()
            .First(c => c.GetProperty("relType").GetString() == "AFFECTS").GetProperty("choiceId").GetString()!;

        var (takeText, takeError) = await server.CallToolAsync("take", new { cursorId, target = affects });
        Assert.False(takeError, takeText);
        Assert.Contains("log-parse", takeText, StringComparison.Ordinal);

        // A made-up target comes back as a tool error, not a crash and not a move.
        var (rejected, rejectedIsError) = await server.CallToolAsync("take", new { cursorId, target = "AFFECTS_EVERYTHING" });
        Assert.True(rejectedIsError);
        Assert.Contains("not_offered", rejected, StringComparison.Ordinal);

        var (walkText, walkError) = await server.CallToolAsync("walk", new { cursorId });
        Assert.False(walkError, walkText);
        using var walk = JsonDocument.Parse(walkText);
        Assert.Equal(1, walk.RootElement.GetProperty("steps").GetArrayLength());

        // stdout is reserved for the protocol: every line is a JSON-RPC 2.0 message.
        Assert.NotEmpty(server.StdoutLines);
        foreach (var line in server.StdoutLines)
        {
            using var message = JsonDocument.Parse(line);
            Assert.Equal("2.0", message.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task Run_query_over_stdio_refuses_a_write()
    {
        using var server = new StdioServer("--seed", "--allow-raw-query");
        await server.InitializeAsync();

        var (text, isError) = await server.CallToolAsync("run_query", new { cypher = "CREATE (:Nope)" });

        Assert.True(isError);
        Assert.Contains("not_read_only", text, StringComparison.Ordinal);
    }
}
