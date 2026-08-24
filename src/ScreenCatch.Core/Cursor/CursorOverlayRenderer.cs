using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Cursor;

public sealed class CursorOverlayRenderer : ICursorOverlay
{
    private const int HighlightRadius = 14;
    private const int RippleRadius = 24;
    private const int StrokeWidth = 2;

    public void Apply(CaptureFrame frame, CursorState state, CursorOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsEnabled)
        {
            return;
        }

        var centerX = state.X - frame.Bounds.X;
        var centerY = state.Y - frame.Bounds.Y;
        if (centerX < 0 || centerY < 0 || centerX >= frame.Width || centerY >= frame.Height)
        {
            return;
        }

        if (options.HighlightEnabled)
        {
            DrawRing(frame, centerX, centerY, HighlightRadius, blue: 0, green: 210, red: 255);
        }

        if (options.ClickEffectsEnabled && state.IsPrimaryButtonDown)
        {
            DrawRing(frame, centerX, centerY, RippleRadius, blue: 255, green: 190, red: 35);
        }
    }

    private static void DrawRing(
        CaptureFrame frame,
        int centerX,
        int centerY,
        int radius,
        byte blue,
        byte green,
        byte red)
    {
        var outer = radius + StrokeWidth;
        var inner = Math.Max(0, radius - StrokeWidth);
        var outerSquared = outer * outer;
        var innerSquared = inner * inner;
        var left = Math.Max(0, centerX - outer);
        var right = Math.Min(frame.Width - 1, centerX + outer);
        var top = Math.Max(0, centerY - outer);
        var bottom = Math.Min(frame.Height - 1, centerY + outer);

        for (var y = top; y <= bottom; y++)
        {
            var dy = y - centerY;
            for (var x = left; x <= right; x++)
            {
                var dx = x - centerX;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared < innerSquared || distanceSquared > outerSquared)
                {
                    continue;
                }

                var offset = (y * frame.Stride) + (x * 4);
                frame.Buffer[offset] = blue;
                frame.Buffer[offset + 1] = green;
                frame.Buffer[offset + 2] = red;
                frame.Buffer[offset + 3] = 255;
            }
        }
    }
}
