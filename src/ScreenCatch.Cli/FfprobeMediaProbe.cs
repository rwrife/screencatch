using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ScreenCatch.Cli;

public sealed record MediaInfo(TimeSpan Duration, int Width, int Height);

public interface IMediaProbe
{
    Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken cancellationToken = default);
}

public sealed class FfprobeMediaProbe : IMediaProbe
{
    private readonly string _ffprobePath;

    public FfprobeMediaProbe(string ffprobePath = "ffprobe")
    {
        _ffprobePath = string.IsNullOrWhiteSpace(ffprobePath)
            ? throw new ArgumentException("ffprobe path is required.", nameof(ffprobePath))
            : ffprobePath;
    }

    public async Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in new[]
                 {
                     "-v", "error", "-show_entries", "format=duration:stream=codec_type,width,height",
                     "-of", "json", inputPath,
                 })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryKill(process);
            throw;
        }

        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException($"ffprobe exited with code {process.ExitCode}: {error.Trim()}");
        }

        return Parse(output);
    }

    public static MediaInfo Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("format", out var format)
            || !format.TryGetProperty("duration", out var durationElement)
            || !TryReadDouble(durationElement, out var durationSeconds)
            || durationSeconds <= 0)
        {
            throw new InvalidDataException("ffprobe did not return a positive media duration.");
        }

        var videoStream = root.GetProperty("streams")
            .EnumerateArray()
            .FirstOrDefault(stream => stream.TryGetProperty("codec_type", out var codec)
                && string.Equals(codec.GetString(), "video", StringComparison.OrdinalIgnoreCase));
        if (videoStream.ValueKind == JsonValueKind.Undefined
            || !videoStream.TryGetProperty("width", out var widthElement)
            || !videoStream.TryGetProperty("height", out var heightElement)
            || !widthElement.TryGetInt32(out var width)
            || !heightElement.TryGetInt32(out var height)
            || width <= 0
            || height <= 0)
        {
            throw new InvalidDataException("ffprobe did not return video dimensions.");
        }

        return new MediaInfo(TimeSpan.FromSeconds(durationSeconds), width, height);
    }

    private static bool TryReadDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        return double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
