using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Cursor;

namespace ScreenCatch.Core.Recording;

public sealed class RecordingSessionRequest
{
    public RecordingSessionRequest(
        CaptureRequest captureRequest,
        VideoEncodeOptions videoOptions,
        AudioCaptureOptions? audioOptions = null,
        CursorOverlayOptions? cursorOverlayOptions = null)
    {
        CaptureRequest = captureRequest ?? throw new ArgumentNullException(nameof(captureRequest));
        VideoOptions = videoOptions ?? throw new ArgumentNullException(nameof(videoOptions));
        AudioOptions = audioOptions;
        CursorOverlayOptions = cursorOverlayOptions;
    }

    public CaptureRequest CaptureRequest { get; }

    public VideoEncodeOptions VideoOptions { get; }

    public AudioCaptureOptions? AudioOptions { get; }

    public CursorOverlayOptions? CursorOverlayOptions { get; }
}
