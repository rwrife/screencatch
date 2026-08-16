namespace ScreenCatch.Core.Capture;

/// <summary>
/// Frame provider abstraction; platform backends implement this to provide full-desktop frames.
/// </summary>
public interface IFrameProvider
{
    CaptureTopology Topology { get; }

    CaptureFrame CaptureFrame(int frameIndex, DateTimeOffset capturedAtUtc);
}
