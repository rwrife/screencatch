using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Presets;

public enum PresetAudioMode
{
    None,
    Microphone,
    System,
    Both,
}

public sealed record RecordingPreset(
    string Name,
    CaptureSourceKind Source = CaptureSourceKind.Screen,
    int FramesPerSecond = 30,
    VideoOutputFormat Format = VideoOutputFormat.Mp4,
    int Quality = 23,
    CaptureRect? Region = null,
    string? MonitorId = null,
    string? WindowTitle = null,
    PresetAudioMode Audio = PresetAudioMode.None)
{
    public RecordingPreset Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Preset name is required.", nameof(Name));
        }

        if (FramesPerSecond is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "FPS must be between 1 and 120.");
        }

        if (Quality is < 0 or > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(Quality), "Quality must be between 0 and 63.");
        }

        if (Source == CaptureSourceKind.Region && Region is null)
        {
            throw new ArgumentException("Region presets require capture bounds.", nameof(Region));
        }

        return this;
    }
}
