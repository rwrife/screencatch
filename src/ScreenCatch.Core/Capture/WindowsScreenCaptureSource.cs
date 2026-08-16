namespace ScreenCatch.Core.Capture;

/// <summary>
/// Windows capture source bootstrap.
/// TODO: replace SyntheticFrameProvider with a Windows.Graphics.Capture backend.
/// </summary>
public sealed class WindowsScreenCaptureSource : SyntheticScreenCaptureSource
{
    public WindowsScreenCaptureSource(CaptureTopology topology)
        : base(new SyntheticFrameProvider(topology))
    {
    }
}
