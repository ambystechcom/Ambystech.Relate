using System.Text.Json;

namespace Ambystech.Relate;

/// <summary>The statement or request is wrong (<c>Neo.ClientError.*</c>): fix it, don't retry it.</summary>
public sealed class RelateClientException : RelateException
{
    internal RelateClientException(string message, string? code, string? gqlStatus, RelateErrorStage stage)
        : base(message, code, gqlStatus, stage, retryable: false) { }
}
