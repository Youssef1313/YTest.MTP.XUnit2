using Xunit;

namespace YTest.MTP.XUnit2.Tests;

public class SimpleTestClass
{
    [Fact]
    public void PassingTest()
    {
    }

    [Fact]
    public void FailingTest()
    {
        Assert.Fail("Failing test...");
    }
}
