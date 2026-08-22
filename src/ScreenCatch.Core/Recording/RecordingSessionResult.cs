using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Recording;

public sealed class RecordingSessionResult
{
    public RecordingSessionResult(
        RecordingSessionState finalState,
        IReadOnlyList<CaptureFrame> capturedFrames,
        VideoEncodeResult encodeResult)
    {
        FinalState = finalState;
        CapturedFrames = capturedFrames ?? Array.Empty<CaptureFrame>();
        EncodeResult = encodeResult ?? throw new ArgumentNullException(nameof(encodeResult));
    }

    public RecordingSessionState FinalState { get; }

    public IReadOnlyList<CaptureFrame> CapturedFrames { get; }

    public VideoEncodeResult EncodeResult { get; }
}
