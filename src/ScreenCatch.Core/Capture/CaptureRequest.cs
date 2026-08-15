namespace ScreenCatch.Core.Capture;

/// <summary>
/// Request for a single capture run.
/// </summary>
public sealed class CaptureRequest
{
    public CaptureRequest(CaptureSourceDescriptor descriptor, int targetFps, int maxFrames)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        if (targetFps is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(targetFps), "FPS must be between 1 and 120.");
        }

        if (maxFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrames), "MaxFrames must be >= 1.");
        }

        Descriptor = descriptor;
        TargetFps = targetFps;
        MaxFrames = maxFrames;
    }

    public CaptureSourceDescriptor Descriptor { get; }

    public int TargetFps { get; }

    public int MaxFrames { get; }

    public TimeSpan FrameInterval => TimeSpan.FromSeconds(1d / TargetFps);
}
