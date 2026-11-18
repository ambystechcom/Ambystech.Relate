---
title: Transactions
description: Explicit and managed transactions, bookmarks and retries.
---

```csharp
using var session = db.OpenSession();

// Explicit
using (var tx = session.BeginTransaction())
{
    tx.Run("CREATE (:Item {n: $n})", new { n = 1 });
    string? bookmark = tx.Commit();          // "relate:<n>", or null when nothing was written
}                                             // disposing without Commit() rolls back

// Managed, retrying transient failures (e.g. write-write conflicts)
var created = session.ExecuteWrite(tx =>
    tx.Run("UNWIND range(1, $n) AS i CREATE (:Item {i: i})", new { n = 3 }).Summary.Counters.NodesCreated);
```

Compare bookmarks with `RelateBookmark.Sequence(bookmark)`, never as strings.

A persistent store has a single writer. See [Threading and store model](/reference/threading-and-store-model/).
