using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class TransactionTests
{
    [Fact]
    public void Transactions_commit_with_bookmark_and_roll_back()
    {
        using var db = RelateDatabase.Open();
        using var session = db.OpenSession();

        string? bookmark;
        using (var tx = session.BeginTransaction())
        {
            tx.Run("CREATE (:Item {n: 1})");
            Assert.True(session.InTransaction);
            bookmark = tx.Commit();
        }

        Assert.NotNull(bookmark);
        Assert.True(RelateBookmark.Sequence(bookmark!) > 0);

        using (var tx = session.BeginTransaction())
        {
            tx.Run("CREATE (:Item {n: 2})");
            // disposed without commit -> rollback
        }

        Assert.False(session.InTransaction);
        Assert.Equal(1L, session.Run("MATCH (i:Item) RETURN count(i) AS n").Scalar<long>());

        using (var tx = session.BeginTransaction())
        {
            tx.Run("MATCH (i:Item) RETURN i");
            Assert.Null(tx.Commit()); // a read-only transaction commits nothing
        }
    }

    [Fact]
    public void ExecuteWrite_commits_and_returns_the_work_value()
    {
        using var db = RelateDatabase.Open();
        using var session = db.OpenSession();

        var created = session.ExecuteWrite(tx =>
            tx.Run("UNWIND range(1, $n) AS i CREATE (:Item {i: i})", new { n = 3 }).Summary.Counters.NodesCreated);

        Assert.Equal(3, created);
        Assert.Equal(3L, session.ExecuteRead(tx => tx.Run("MATCH (i:Item) RETURN count(i)").Scalar<long>()));
    }
}
