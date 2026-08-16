namespace ScreenCatch.Core.Capture;

/// <summary>
/// macOS capture source bootstrap.
/// TODO: replace SyntheticFrameProvider with a ScreenCaptureKit backend.
/// </summary>
public sealed class MacScreenCaptureSource : SyntheticScreenCaptureSource
{
    public MacScreenCaptureSource(CaptureTopology topology)
        : base(new SyntheticFrameProvider(topology))
    {
    }
}
