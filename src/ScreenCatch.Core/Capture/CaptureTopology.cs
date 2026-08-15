namespace ScreenCatch.Core.Capture;

/// <summary>
/// Live topology snapshot used for descriptor resolution and validation.
/// </summary>
public sealed class CaptureTopology
{
    public CaptureTopology(
        CaptureRect virtualDesktopBounds,
        IReadOnlyDictionary<string, CaptureRect> monitors,
        IReadOnlyList<CaptureWindowInfo> windows)
    {
        VirtualDesktopBounds = virtualDesktopBounds;
        Monitors = new Dictionary<string, CaptureRect>(
            monitors ?? throw new ArgumentNullException(nameof(monitors)),
            StringComparer.OrdinalIgnoreCase);
        Windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    public CaptureRect VirtualDesktopBounds { get; }

    public IReadOnlyDictionary<string, CaptureRect> Monitors { get; }

    public IReadOnlyList<CaptureWindowInfo> Windows { get; }

    public static CaptureTopology CreateDefaultForTests()
    {
        var desktop = new CaptureRect(0, 0, 1920, 1080);
        var monitors = new Dictionary<string, CaptureRect>(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = new CaptureRect(0, 0, 1280, 720),
            ["secondary"] = new CaptureRect(1280, 0, 640, 720),
        };

        var windows = new List<CaptureWindowInfo>
        {
            new("terminal", "Terminal", new CaptureRect(64, 64, 900, 500)),
            new("browser", "Browser", new CaptureRect(128, 120, 1200, 700)),
        };

        return new CaptureTopology(desktop, monitors, windows);
    }
}

public sealed record CaptureWindowInfo(string Id, string Title, CaptureRect Bounds);
