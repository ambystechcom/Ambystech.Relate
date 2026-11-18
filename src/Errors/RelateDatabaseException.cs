using System.Text.Json;

namespace Ambystech.Relate;

/// <summary>The engine failed (<c>Neo.DatabaseError.*</c>).</summary>
public sealed class RelateDatabaseException : RelateException
{
    internal RelateDatabaseException(string message, string? code, string? gqlStatus, RelateErrorStage stage)
        : base(message, code, gqlStatus, stage, retryable: false) { }
}
