using ScreenCatch.Core.Editing;

namespace ScreenCatch.Core.Tests;

public sealed class TrimRangeTests
{
    [Fact]
    public void Constructor_ComputesDurationFromInAndOutPoints()
    {
        var range = new TrimRange(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(8));

        Assert.Equal(TimeSpan.FromSeconds(5.5), range.Duration);
    }

    [Fact]
    public void Constructor_RejectsNegativeInPoint()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrimRange(TimeSpan.FromMilliseconds(-1), TimeSpan.FromSeconds(1)));

        Assert.Equal("start", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsOutPointAtOrBeforeInPoint()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrimRange(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

        Assert.Equal("end", error.ParamName);
    }
}
