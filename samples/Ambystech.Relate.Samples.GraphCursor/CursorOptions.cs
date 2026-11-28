using System.Globalization;

namespace Ambystech.Relate.Samples.GraphCursor;

/// <summary>Server configuration, parsed from the command line.</summary>
public sealed class CursorOptions
{
    public const string DefaultStoreName = "default";

    /// <summary>Stores the agent may open, by name. <c>open(store)</c> accepts only these names, never a path.</summary>
    public Dictionary<string, string> Stores { get; } = new(StringComparer.Ordinal);

    /// <summary>Load the demo dataset into every configured store at startup.</summary>
    public bool Seed { get; set; }

    /// <summary>Audit store path (a <c>.rl</c> file). Null keeps the audit graph in a private in-memory store.</summary>
    public string? AuditPath { get; set; }

    /// <summary>Expose <c>run_query</c> and accept raw Cypher as a start query.</summary>
    public bool AllowRawQuery { get; set; }

    /// <summary>Open the stores read-only (incompatible with <see cref="Seed"/>).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Hard cap on nodes returned by any single call.</summary>
    public int MaxNodes { get; set; } = 50;

    /// <summary>Hard cap on the serialized size of <c>gather</c> output.</summary>
    public int MaxTextBytes { get; set; } = 4096;

    /// <summary>The most choices <c>next_hop</c> returns.</summary>
    public int MaxChoices { get; set; } = 12;

    /// <summary>The most rows <c>run_query</c> returns.</summary>
    public int RowCap { get; set; } = 100;

    /// <summary>Wall-clock limit for <c>run_query</c>. The engine cannot cancel a running statement.</summary>
    public int QueryTimeoutMilliseconds { get; set; } = 5000;

    public static string Usage =>
        """
        graph-cursor: an MCP server whose main tool is next_hop, not run_query.

        Options:
          --store <name=path>       A store the agent may open (repeatable). A bare path is named 'default'.
                                    Default: default=:memory:
          --seed                    Load the demo dataset (idempotent) into each store at startup.
          --audit <path>            Write the audit graph to this .rl file (default: in-memory).
          --allow-raw-query         Enable run_query and raw-Cypher start queries (off by default).
          --read-only               Open the stores read-only.
          --max-nodes <n>           Nodes per call (default 50).
          --max-text-bytes <n>      Bytes of gather output (default 4096).
          --max-choices <n>         Choices per next_hop (default 12).
          --row-cap <n>             Rows from run_query (default 100).
          --query-timeout-ms <n>    run_query wall-clock limit (default 5000).
        """;

    public static CursorOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CursorOptions();
        for (var i = 0; i < args.Count; i++)
        {
            string Next() => ++i < args.Count ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value.");

            switch (args[i])
            {
                case "--store":
                    var spec = Next();
                    var eq = spec.IndexOf('=', StringComparison.Ordinal);
                    if (eq > 0)
                    {
                        options.Stores[spec[..eq]] = spec[(eq + 1)..];
                    }
                    else
                    {
                        options.Stores[DefaultStoreName] = spec;
                    }

                    break;
                case "--seed":
                    options.Seed = true;
                    break;
                case "--audit":
                    options.AuditPath = Next();
                    break;
                case "--allow-raw-query":
                    options.AllowRawQuery = true;
                    break;
                case "--read-only":
                    options.ReadOnly = true;
                    break;
                case "--max-nodes":
                    options.MaxNodes = PositiveInt(args[i], Next());
                    break;
                case "--max-text-bytes":
                    options.MaxTextBytes = PositiveInt(args[i], Next());
                    break;
                case "--max-choices":
                    options.MaxChoices = PositiveInt(args[i], Next());
                    break;
                case "--row-cap":
                    options.RowCap = PositiveInt(args[i], Next());
                    break;
                case "--query-timeout-ms":
                    options.QueryTimeoutMilliseconds = PositiveInt(args[i], Next());
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (options.Stores.Count == 0)
        {
            options.Stores[DefaultStoreName] = RelateDatabase.InMemory;
        }

        if (options.Seed && options.ReadOnly)
        {
            throw new ArgumentException("--seed cannot be combined with --read-only.");
        }

        return options;
    }

    private static int PositiveInt(string flag, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : throw new ArgumentException($"{flag} needs a positive integer, got '{value}'.");
}
