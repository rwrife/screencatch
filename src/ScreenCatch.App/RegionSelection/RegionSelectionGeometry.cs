using Avalonia;
using ScreenCatch.Core.Capture;

namespace ScreenCatch.App.RegionSelection;

public static class RegionSelectionGeometry
{
    public static CaptureRect ToCaptureRect(
        Point dragStart,
        Point dragEnd,
        PixelPoint virtualDesktopOrigin,
        double renderScaling)
    {
        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        var left = Math.Min(dragStart.X, dragEnd.X);
        var top = Math.Min(dragStart.Y, dragEnd.Y);
        var width = Math.Abs(dragEnd.X - dragStart.X);
        var height = Math.Abs(dragEnd.Y - dragStart.Y);

        var x = virtualDesktopOrigin.X + RoundToPixel(left * renderScaling);
        var y = virtualDesktopOrigin.Y + RoundToPixel(top * renderScaling);
        var pixelWidth = Math.Max(1, RoundToPixel(width * renderScaling));
        var pixelHeight = Math.Max(1, RoundToPixel(height * renderScaling));
        return new CaptureRect(x, y, pixelWidth, pixelHeight);
    }

    private static int RoundToPixel(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
}
