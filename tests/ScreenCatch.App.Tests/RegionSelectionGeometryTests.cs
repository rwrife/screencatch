using Avalonia;
using ScreenCatch.App.RegionSelection;
using ScreenCatch.Core.Capture;

namespace ScreenCatch.App.Tests;

public sealed class RegionSelectionGeometryTests
{
    [Fact]
    public void ToCaptureRect_NormalizesDragAndConvertsDipsToVirtualDesktopPixels()
    {
        var result = RegionSelectionGeometry.ToCaptureRect(
            dragStart: new Point(500, 300),
            dragEnd: new Point(20, 30),
            virtualDesktopOrigin: new PixelPoint(-1920, -200),
            renderScaling: 1.5);

        Assert.Equal(new CaptureRect(-1890, -155, 720, 405), result);
    }

    [Fact]
    public void ToCaptureRect_ClampsSubPixelSelectionToOnePixel()
    {
        var result = RegionSelectionGeometry.ToCaptureRect(
            dragStart: new Point(10, 10),
            dragEnd: new Point(10.1, 10.1),
            virtualDesktopOrigin: new PixelPoint(0, 0),
            renderScaling: 1.25);

        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }
}
