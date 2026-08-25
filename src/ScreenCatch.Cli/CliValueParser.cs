using System.Globalization;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Cli;

public static class CliValueParser
{
    public static RecordingPreset BuildPreset(CliInvocation invocation, RecordingPreset? saved = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var baseline = saved ?? new RecordingPreset("command-line");

        var source = invocation.Get("source") is { } sourceValue
            ? ParseSource(sourceValue)
            : baseline.Source;
        var fps = invocation.Get("fps") is { } fpsValue
            ? ParseInt(fpsValue, "fps", 1, 120)
            : baseline.FramesPerSecond;
        var format = invocation.Get("format") is { } formatValue
            ? ParseVideoFormat(formatValue)
            : baseline.Format;
        var quality = invocation.Get("quality") is { } qualityValue
            ? ParseInt(qualityValue, "quality", 0, 63)
            : baseline.Quality;
        var region = invocation.Get("rect") is { } rectValue
            ? ParseRect(rectValue)
            : baseline.Region;
        var audio = invocation.Get("audio") is { } audioValue
            ? ParseAudio(audioValue)
            : baseline.Audio;

        return new RecordingPreset(
            baseline.Name,
            source,
            fps,
            format,
            quality,
            region,
            invocation.Get("monitor") ?? baseline.MonitorId,
            invocation.Get("title") ?? baseline.WindowTitle,
            audio).Validate();
    }

    public static CaptureRect ParseRect(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            throw new CliUsageException("--rect must be X,Y,WIDTH,HEIGHT using integers.");
        }

        try
        {
            return new CaptureRect(x, y, width, height);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new CliUsageException($"Invalid --rect: {exception.Message}");
        }
    }

    public static TimeSpan ParseTime(string value, string optionName)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero)
        {
            return time;
        }

        throw new CliUsageException($"--{optionName} must be non-negative seconds or a clock value.");
    }

    public static int ParseInt(string value, string optionName, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            || result < minimum
            || result > maximum)
        {
            throw new CliUsageException($"--{optionName} must be between {minimum} and {maximum}.");
        }

        return result;
    }

    private static CaptureSourceKind ParseSource(string value) => value.ToLowerInvariant() switch
    {
        "screen" => CaptureSourceKind.Screen,
        "monitor" => CaptureSourceKind.Monitor,
        "window" => CaptureSourceKind.Window,
        "region" => CaptureSourceKind.Region,
        _ => throw new CliUsageException("--source must be screen, monitor, window, or region."),
    };

    private static VideoOutputFormat ParseVideoFormat(string value) => value.ToLowerInvariant() switch
    {
        "mp4" => VideoOutputFormat.Mp4,
        "webm" => VideoOutputFormat.WebM,
        _ => throw new CliUsageException("--format must be mp4 or webm for recording."),
    };

    private static PresetAudioMode ParseAudio(string value) => value.ToLowerInvariant() switch
    {
        "none" => PresetAudioMode.None,
        "mic" or "microphone" => PresetAudioMode.Microphone,
        "system" => PresetAudioMode.System,
        "both" => PresetAudioMode.Both,
        _ => throw new CliUsageException("--audio must be none, mic, system, or both."),
    };
}
