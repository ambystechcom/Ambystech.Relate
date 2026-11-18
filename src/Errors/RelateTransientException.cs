using System.Text.Json;

namespace Ambystech.Relate;

/// <summary>A transient failure the engine marked retryable, such as a write-write conflict.</summary>
public sealed class RelateTransientException : RelateException
{
    internal RelateTransientException(string message, string? code, string? gqlStatus, RelateErrorStage stage)
        : base(message, code, gqlStatus, stage, retryable: true) { }
}
