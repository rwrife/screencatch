using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Cursor;

namespace ScreenCatch.Core.Tests;

public sealed class CursorOverlayRendererTests
{
    [Fact]
    public void Apply_DrawsHighlightRingAroundCursor()
    {
        var frame = CreateFrame(48, 48);
        var renderer = new CursorOverlayRenderer();

        renderer.Apply(
            frame,
            new CursorState(24, 24, IsPrimaryButtonDown: false),
            new CursorOverlayOptions(HighlightEnabled: true, ClickEffectsEnabled: false));

        AssertPixelChanged(frame, 24, 10);
        AssertPixelUnchanged(frame, 24, 24);
    }

    [Fact]
    public void Apply_DrawsClickRippleWhenPrimaryButtonIsDown()
    {
        var frame = CreateFrame(64, 64);
        var renderer = new CursorOverlayRenderer();

        renderer.Apply(
            frame,
            new CursorState(32, 32, IsPrimaryButtonDown: true),
            new CursorOverlayOptions(HighlightEnabled: false, ClickEffectsEnabled: true));

        AssertPixelChanged(frame, 32, 8);
    }

    [Fact]
    public void Apply_LeavesFrameUntouchedWhenEffectsAreDisabled()
    {
        var frame = CreateFrame(32, 32);
        var renderer = new CursorOverlayRenderer();

        renderer.Apply(
            frame,
            new CursorState(16, 16, IsPrimaryButtonDown: true),
            new CursorOverlayOptions(HighlightEnabled: false, ClickEffectsEnabled: false));

        Assert.All(frame.Buffer, value => Assert.Equal((byte)0, value));
    }

    private static CaptureFrame CreateFrame(int width, int height)
    {
        var stride = width * 4;
        return new CaptureFrame(
            DateTimeOffset.UtcNow,
            new CaptureRect(0, 0, width, height),
            stride,
            new byte[stride * height]);
    }

    private static void AssertPixelChanged(CaptureFrame frame, int x, int y)
    {
        var offset = (y * frame.Stride) + (x * 4);
        Assert.NotEqual(0, frame.Buffer[offset] + frame.Buffer[offset + 1] + frame.Buffer[offset + 2]);
    }

    private static void AssertPixelUnchanged(CaptureFrame frame, int x, int y)
    {
        var offset = (y * frame.Stride) + (x * 4);
        Assert.Equal(0, frame.Buffer[offset] + frame.Buffer[offset + 1] + frame.Buffer[offset + 2]);
    }
}
