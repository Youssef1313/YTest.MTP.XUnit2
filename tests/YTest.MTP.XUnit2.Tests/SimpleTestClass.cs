using Xunit;

namespace YTest.MTP.XUnit2.Tests;

public class SimpleTestClass
{
    [Fact]
    public void PassingTest()
    {
    }

    [Fact(Skip = "Skipped becase...")]
    public void SkippedTet()
    {
        Assert.Fail("Failing test...");
    }
}
