# Type mapping

| Cypher | .NET (read) | .NET (parameter) |
| --- | --- | --- |
| `NULL` | `null` (`DBNull` in ADO.NET) | `null`, `DBNull` |
| `BOOLEAN` | `bool` | `bool` |
| `INTEGER` | `long` | any integral type |
| `FLOAT` | `double` | `float`, `double`, `decimal` |
| `STRING` | `string` | `string`, `char`, `Guid`, enums |
| `LIST` | `List<object?>` | any `IEnumerable`; `byte[]` becomes a list of ints |
| `MAP` | `Dictionary<string, object?>` | `IDictionary`, `IEnumerable<KeyValuePair<string, object?>>` |
| `NODE` / `RELATIONSHIP` / `PATH` | `RelateNode` / `RelateRelationship` / `RelatePath` | n/a |
| `DATE` | `DateOnly` | `DateOnly` |
| `LOCAL TIME` | `TimeOnly`¹ | `TimeOnly` |
| `LOCAL DATETIME` | `DateTime` (Unspecified)¹ | `DateTime` (Local/Unspecified) |
| `ZONED DATETIME` | `DateTimeOffset`¹ | `DateTimeOffset`, `DateTime` (Utc), `RelateTemporal` with zone |
| `ZONED TIME` | `RelateTemporal` | `RelateTemporal.OffsetTime(...)` |
| `DURATION` | `RelateDuration` | `TimeSpan`, `RelateDuration` |
| `POINT` | `RelatePoint` | `RelatePoint` |
| `VECTOR` | `RelateVector` | `RelateVector` (a plain `float[]` binds as a `LIST`) |

¹ When the value has sub-100 ns precision, a named IANA zone or a non-minute offset, it comes back as a lossless `RelateTemporal` instead. `Get<T>()` converts between compatible types (`long` to `int`, `RelateVector` to `float[]`, `RelateDuration` to `TimeSpan`, and so on).
