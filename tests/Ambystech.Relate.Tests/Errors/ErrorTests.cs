using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class ErrorTests
{
    [Fact]
    public void Statement_errors_are_classified()
    {
        using var db = RelateDatabase.Open();

        var syntax = Assert.Throws<RelateClientException>(() => db.Run("MATCH (n RETURN n"));
        Assert.StartsWith("Neo.ClientError.", syntax.Code);
        Assert.Equal(RelateErrorStage.Compile, syntax.Stage);
        Assert.False(syntax.Retryable);
        Assert.False(string.IsNullOrEmpty(syntax.GqlStatus));

        var missing = Assert.Throws<RelateClientException>(() => db.Run("RETURN $nope AS x"));
        Assert.Contains("nope", missing.Message, StringComparison.OrdinalIgnoreCase);
    }
}
