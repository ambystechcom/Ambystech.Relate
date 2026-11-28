using Ambystech.Relate.Samples.GraphCursor.Audit;
using Ambystech.Relate.Samples.GraphCursor.Cursor;
using Ambystech.Relate.Samples.GraphCursor.Scoring;
using Ambystech.Relate.Samples.GraphCursor.Seed;

namespace Ambystech.Relate.Samples.GraphCursor.Tests;

/// <summary>An in-memory seeded store, a private in-memory audit store and a cursor service wired over them.</summary>
internal sealed class CursorFixture : IDisposable
{
    public const string PlantedCve = "(:CVE {id: 'CVE-2026-1234'})";

    private readonly RelateDatabase _data;
    private readonly StoreRegistry _stores;

    public CursorFixture(Action<CursorOptions>? configure = null)
    {
        Options = new CursorOptions();
        Options.Stores[CursorOptions.DefaultStoreName] = RelateDatabase.InMemory;
        configure?.Invoke(Options);

        _data = RelateDatabase.Open();
        SeedLoader.Load(_data);
        _stores = new StoreRegistry(new Dictionary<string, RelateDatabase> { [CursorOptions.DefaultStoreName] = _data });
        Audit = AuditLog.Open(null);
        Service = new CursorService(_stores, Options, new TokenOverlapScorer(), Audit, TimeProvider.System);
    }

    public CursorOptions Options { get; }

    public RelateDatabase Data => _data;

    public AuditLog Audit { get; }

    public CursorService Service { get; }

    public void Dispose()
    {
        Audit.Dispose();
        _stores.Dispose();
        _data.Dispose();
    }

    /// <summary>The choice with the given relationship type and direction; fails the test if it is missing.</summary>
    public static ChoiceView Choice(NextHopResult hop, string relType, string direction) =>
        hop.Choices.Single(c => c.RelType == relType && c.Direction == direction);
}
