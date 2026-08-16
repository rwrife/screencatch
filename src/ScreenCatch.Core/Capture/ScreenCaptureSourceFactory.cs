namespace ScreenCatch.Core.Capture;

public static class ScreenCaptureSourceFactory
{
    /// <summary>
    /// Creates an OS-appropriate source. On non-target OSes, this returns
    /// a deterministic synthetic source for development and tests.
    /// </summary>
    public static IScreenCaptureSource CreateDefault(CaptureTopology? topology = null)
    {
        topology ??= CaptureTopology.CreateDefaultForTests();

        if (OperatingSystem.IsWindows())
        {
            return new WindowsScreenCaptureSource(topology);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacScreenCaptureSource(topology);
        }

        return new SyntheticScreenCaptureSource(new SyntheticFrameProvider(topology));
    }
}
