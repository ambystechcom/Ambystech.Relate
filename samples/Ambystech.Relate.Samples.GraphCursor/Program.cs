using Ambystech.Relate.Samples.GraphCursor;
using Ambystech.Relate.Samples.GraphCursor.Audit;
using Ambystech.Relate.Samples.GraphCursor.Cursor;
using Ambystech.Relate.Samples.GraphCursor.Scoring;
using Ambystech.Relate.Samples.GraphCursor.Seed;
using Ambystech.Relate.Samples.GraphCursor.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

CursorOptions options;
try
{
    options = CursorOptions.Parse(args);
}
catch (ArgumentException e)
{
    // stdout is reserved for the MCP protocol: everything human-readable goes to stderr.
    await Console.Error.WriteLineAsync(e.Message);
    await Console.Error.WriteLineAsync(CursorOptions.Usage);
    return 2;
}

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
// The SDK logs every request at Information; keep stderr to what an operator needs.
builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

using var stores = new StoreRegistry(options);
if (options.Seed)
{
    foreach (var name in stores.Names)
    {
        var loaded = SeedLoader.Load(stores.Get(name));
        await Console.Error.WriteLineAsync($"store '{name}': demo dataset {(loaded ? "loaded" : "already present")}");
    }
}

using var audit = AuditLog.Open(options.AuditPath);

builder.Services
    .AddSingleton(options)
    .AddSingleton(stores)
    .AddSingleton(audit)
    .AddSingleton(TimeProvider.System)
    .AddSingleton<IHopScorer, TokenOverlapScorer>()
    .AddSingleton<CursorService>();

var mcp = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CursorTools>();
if (options.AllowRawQuery)
{
    mcp.WithTools<RawQueryTools>();
}

await Console.Error.WriteLineAsync(
    $"graph-cursor ready: stores [{string.Join(", ", stores.Names)}], audit {options.AuditPath ?? "in-memory"}, " +
    $"run_query {(options.AllowRawQuery ? "ENABLED" : "disabled")}");

await builder.Build().RunAsync();
return 0;
