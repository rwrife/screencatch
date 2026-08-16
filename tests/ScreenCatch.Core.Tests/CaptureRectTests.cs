using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Tests;

public sealed class CaptureRectTests
{
    [Fact]
    public void Contains_ReturnsTrue_ForInnerRect()
    {
        var outer = new CaptureRect(0, 0, 100, 100);
        var inner = new CaptureRect(10, 10, 20, 20);

        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Intersect_ReturnsOverlap_Rect()
    {
        var a = new CaptureRect(0, 0, 100, 100);
        var b = new CaptureRect(80, 80, 50, 50);

        var overlap = a.Intersect(b);

        Assert.Equal(new CaptureRect(80, 80, 20, 20), overlap);
    }

    [Fact]
    public void Intersect_Throws_WhenNoOverlap()
    {
        var a = new CaptureRect(0, 0, 100, 100);
        var b = new CaptureRect(200, 200, 25, 25);

        Assert.Throws<InvalidOperationException>(() => a.Intersect(b));
    }
}
