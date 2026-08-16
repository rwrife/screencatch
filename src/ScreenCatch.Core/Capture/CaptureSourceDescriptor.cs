namespace ScreenCatch.Core.Capture;

/// <summary>
/// Capture source selector model.
/// </summary>
public abstract record CaptureSourceDescriptor(CaptureSourceKind Kind);

public sealed record FullScreenCaptureDescriptor()
    : CaptureSourceDescriptor(CaptureSourceKind.Screen);

public sealed record MonitorCaptureDescriptor(string MonitorId)
    : CaptureSourceDescriptor(CaptureSourceKind.Monitor);

public sealed record WindowCaptureDescriptor(string? WindowId = null, string? WindowTitle = null)
    : CaptureSourceDescriptor(CaptureSourceKind.Window);

public sealed record RegionCaptureDescriptor(CaptureRect Region)
    : CaptureSourceDescriptor(CaptureSourceKind.Region);
