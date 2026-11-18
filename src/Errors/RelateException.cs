using System.Text.Json;

namespace Ambystech.Relate;

/// <summary>
/// Base exception for Relate. Engine failures carry the ADR-0502 §2 error shape:
/// Neo4j status <see cref="Code"/>, <see cref="GqlStatus"/>, <see cref="Stage"/> and <see cref="Retryable"/>.
/// </summary>
public class RelateException : Exception
{
    /// <summary>Create a binding-side failure without engine classification.</summary>
    public RelateException(string message) : base(message) { }

    /// <summary>Create a binding-side failure without engine classification.</summary>
    public RelateException(string message, Exception? innerException) : base(message, innerException) { }

    /// <summary>Create a classified engine failure.</summary>
    public RelateException(string message, string? code, string? gqlStatus, RelateErrorStage stage, bool retryable)
        : base(message)
    {
        Code = code;
        GqlStatus = gqlStatus;
        Stage = stage;
        Retryable = retryable;
    }

    /// <summary>Neo4j status code, e.g. <c>Neo.ClientError.Statement.SyntaxError</c>.</summary>
    public string? Code { get; }

    /// <summary>The GQLSTATUS code, e.g. <c>42001</c>.</summary>
    public string? GqlStatus { get; }

    /// <summary>Where in the engine the failure happened.</summary>
    public RelateErrorStage Stage { get; }

    /// <summary>True when retrying the same work may succeed (e.g. a write-write conflict).</summary>
    public bool Retryable { get; }

    internal static RelateException FromErrorJson(string? json, string operation)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new RelateException($"{operation} failed (no error detail).");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var gql = root.TryGetProperty("gql_status", out var g) ? g.GetString() : null;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            var retryable = root.TryGetProperty("retryable", out var r) && r.ValueKind == JsonValueKind.True;
            var stage = root.TryGetProperty("stage", out var s) ? ParseStage(s.GetString()) : RelateErrorStage.Unknown;
            message ??= $"{operation} failed.";

            if (retryable)
            {
                return new RelateTransientException(message, code, gql, stage);
            }

            if (code is not null && code.StartsWith("Neo.ClientError.", StringComparison.Ordinal))
            {
                return new RelateClientException(message, code, gql, stage);
            }

            if (code is not null && code.StartsWith("Neo.DatabaseError.", StringComparison.Ordinal))
            {
                return new RelateDatabaseException(message, code, gql, stage);
            }

            return new RelateException(message, code, gql, stage, retryable: false);
        }
        catch (JsonException e)
        {
            return new RelateException($"{operation} failed with a malformed error shape: {json}", e);
        }
    }

    private static RelateErrorStage ParseStage(string? stage) => stage switch
    {
        "compile" => RelateErrorStage.Compile,
        "runtime" => RelateErrorStage.Runtime,
        "internal" => RelateErrorStage.Internal,
        _ => RelateErrorStage.Unknown,
    };
}
