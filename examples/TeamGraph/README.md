# TeamGraph example

A console app showing the main `Ambystech.Relate` features: seeding a graph, parameterized queries, paths,
transactions and bookmarks, error classification, and the ADO.NET provider.

```bash
scripts/fetch-natives.sh 1.0.2                                # once: stage the native engine
dotnet run --project examples/TeamGraph                       # private in-memory graph
dotnet run --project examples/TeamGraph -- ./team.rl          # persisted; also shows ADO.NET
```

A store has a single writer, so the demo closes its driver database before the ADO.NET section opens the same file.
