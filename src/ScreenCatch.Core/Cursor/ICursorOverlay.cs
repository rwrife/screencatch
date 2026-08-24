using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Cursor;

public interface ICursorOverlay
{
    void Apply(CaptureFrame frame, CursorState state, CursorOverlayOptions options);
}
