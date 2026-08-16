namespace ScreenCatch.Core.Capture;

public sealed record CaptureProgress(int FramesCaptured, int FramesTarget, TimeSpan Elapsed);
