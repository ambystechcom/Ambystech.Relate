using System.Diagnostics;
using System.Text.Json;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

/// <summary>Runs the built MCP server as a child process and talks JSON-RPC to it over stdio.</summary>
internal sealed class StdioServer : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly Process _process;
    private readonly List<string> _stderr = [];
    private int _nextId;

    public StdioServer(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(ServerDll());
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        _process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the server.");
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_stderr)
                {
                    _stderr.Add(e.Data);
                }
            }
        };
        _process.BeginErrorReadLine();
    }

    /// <summary>Every line the server wrote to stdout. The protocol owns stdout, so each must be a JSON-RPC message.</summary>
    public List<string> StdoutLines { get; } = [];

    public string Stderr
    {
        get
        {
            lock (_stderr)
            {
                return string.Join('\n', _stderr);
            }
        }
    }

    public async Task InitializeAsync()
    {
        await RequestAsync("initialize", new
        {
            protocolVersion = "2025-03-26",
            capabilities = new { },
            clientInfo = new { name = "graph-cursor-smoke-test", version = "1.0" },
        });
        await SendAsync(new { jsonrpc = "2.0", method = "notifications/initialized" });
    }

    public async Task<JsonElement> RequestAsync(string method, object? parameters = null)
    {
        var id = ++_nextId;
        await SendAsync(new { jsonrpc = "2.0", id, method, @params = parameters });

        using var cts = new CancellationTokenSource(Timeout);
        while (true)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cts.Token)
                ?? throw new InvalidOperationException($"The server closed stdout. stderr:\n{Stderr}");
            StdoutLines.Add(line);
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
            {
                return doc.RootElement.Clone();
            }
        }
    }

    /// <summary>Call a tool and return the JSON text it produced, or the error text with <paramref name="isError"/> set.</summary>
    public async Task<(string Text, bool IsError)> CallToolAsync(string name, object arguments)
    {
        var response = await RequestAsync("tools/call", new { name, arguments });
        var result = response.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return (text, result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
    }

    public void Dispose()
    {
        try
        {
            _process.StandardInput.Close();
            if (!_process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }

        _process.Dispose();
    }

    private async Task SendAsync(object message)
    {
        await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>The server's own build output, found next to this test project's output (same configuration).</summary>
    private static string ServerDll()
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var tfm = baseDir.Name;
        var configuration = baseDir.Parent!.Name;
        var samples = baseDir.Parent.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(samples, "Ambystech.Relate.Samples.GraphCursor", "bin", configuration, tfm, "Ambystech.Relate.Samples.GraphCursor.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("The server has not been built.", dll);
    }
}
