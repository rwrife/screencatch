using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Recording;

public sealed class RecordingSessionRequest
{
    public RecordingSessionRequest(
        CaptureRequest captureRequest,
        VideoEncodeOptions videoOptions,
        AudioCaptureOptions? audioOptions = null)
    {
        CaptureRequest = captureRequest ?? throw new ArgumentNullException(nameof(captureRequest));
        VideoOptions = videoOptions ?? throw new ArgumentNullException(nameof(videoOptions));
        AudioOptions = audioOptions;
    }

    public CaptureRequest CaptureRequest { get; }

    public VideoEncodeOptions VideoOptions { get; }

    public AudioCaptureOptions? AudioOptions { get; }
}
