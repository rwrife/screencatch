using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Recording;

public sealed record RecordingSessionProgress(
    RecordingSessionState State,
    CaptureProgress? CaptureProgress = null,
    VideoEncoderProgress? EncodingProgress = null);
