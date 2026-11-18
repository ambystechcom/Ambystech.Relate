using System.Data;
using System.Data.Common;

namespace Ambystech.Relate.Data;

/// <summary>An ADO.NET transaction over a <see cref="RelateTransaction"/>.</summary>
public sealed class RelateDbTransaction : DbTransaction
{
    private readonly RelateDbConnection _connection;

    internal RelateDbTransaction(RelateDbConnection connection, RelateTransaction transaction, IsolationLevel isolationLevel)
    {
        _connection = connection;
        Transaction = transaction;
        IsolationLevel = isolationLevel == IsolationLevel.Unspecified ? IsolationLevel.Snapshot : isolationLevel;
    }

    /// <summary>The driver transaction.</summary>
    public RelateTransaction Transaction { get; }

    /// <summary>The commit bookmark (<c>relate:&lt;n&gt;</c>) after a commit that wrote something.</summary>
    public string? Bookmark { get; private set; }

    /// <summary>Relate commits optimistically against a snapshot.</summary>
    public override IsolationLevel IsolationLevel { get; }

    /// <inheritdoc />
    protected override DbConnection DbConnection => _connection;

    internal bool IsCompleted => !Transaction.IsOpen;

    /// <inheritdoc />
    public override void Commit()
    {
        Bookmark = Transaction.Commit();
        Detach();
    }

    /// <inheritdoc />
    public override void Rollback()
    {
        Transaction.Rollback();
        Detach();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsCompleted)
        {
            Rollback();
        }

        base.Dispose(disposing);
    }

    private void Detach()
    {
        if (ReferenceEquals(_connection.CurrentTransaction, this))
        {
            _connection.CurrentTransaction = null;
        }
    }
}
