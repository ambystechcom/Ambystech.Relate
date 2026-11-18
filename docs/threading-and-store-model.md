# Threading and store model

- `RelateDatabase` is thread-safe; share one per store.
- `RelateSession` serializes its own calls. Use one session per concurrent unit of work; distinct sessions run in parallel.
- A persistent store has **one writer**: a second writable open of the same path, in or out of process, fails with a locked-store error. A `.rl` file also locks out read-only opens while a writer has it, while a directory store can be read beside its writer.
- The whole graph lives in memory. There's no paging, replication, users or roles. See [Relate's limitations](https://relatedb.ai/docs/reference/limitations.md).
- The engine is synchronous and in-process. `RunAsync` only moves the work to the thread pool, and cancellation is checked before the statement starts.
