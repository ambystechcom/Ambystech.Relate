---
title: Errors
description: How engine failures surface as exceptions.
---

Engine failures throw `RelateException` with the engine's classification: `Code` (Neo4j status code), `GqlStatus`, `Stage` (`Compile`, `Runtime`, `Internal`) and `Retryable`. Subclasses:

- `RelateClientException` for `Neo.ClientError.*`: fix the statement.
- `RelateTransientException`: retryable.
- `RelateDatabaseException` for `Neo.DatabaseError.*`.
