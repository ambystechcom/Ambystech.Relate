using System.Diagnostics.CodeAnalysis;

namespace Ambystech.Relate;

/// <summary>
/// An explicit transaction on a <see cref="RelateSession"/>. Disposing without
/// <see cref="Commit"/> rolls back. Relate has no nested transactions or savepoints.
/// </summary>
public sealed class RelateTransaction : IDisposable
{
    private readonly RelateSession _session;
    private volatile bool _open = true;

    internal RelateTransaction(RelateSession session) => _session = session;

    /// <summary>True until committed or rolled back.</summary>
    public bool IsOpen => _open;

    /// <summary>Run a statement inside this transaction.</summary>
    public RelateResult Run(string cypher, IReadOnlyDictionary<string, object?>? parameters = null) =>
        _session.RunInTransaction(this, cypher, parameters);

    /// <summary>Run a statement inside this transaction, with parameters from an object's public properties.</summary>
    public RelateResult Run<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TParameters>(
        string cypher, TParameters parameters)
        where TParameters : class =>
        _session.RunInTransaction(this, cypher, RelateSession.ToDictionary(parameters));

    /// <summary>
    /// Commit. Returns the bookmark (<c>relate:&lt;n&gt;</c>, monotonic per store; compare
    /// with <see cref="RelateBookmark.Sequence"/>), or null when the transaction wrote nothing.
    /// </summary>
    public string? Commit() => _session.Commit(this);

    /// <summary>Discard the transaction's changes. A no-op once closed.</summary>
    public void Rollback() => _session.Rollback(this);

    /// <summary>Roll back if still open.</summary>
    public void Dispose() => Rollback();

    internal void MarkClosed() => _open = false;
}
