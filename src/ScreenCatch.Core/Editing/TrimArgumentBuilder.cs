using System.Globalization;

namespace ScreenCatch.Core.Editing;

public static class TrimArgumentBuilder
{
    public static IReadOnlyList<string> Build(TrimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-ss", FormatSeconds(request.Range.Start),
            "-i", request.InputPath,
            "-t", FormatSeconds(request.Range.Duration),
        };

        if (HasMatchingContainer(request.InputPath, request.OutputPath) && !request.RequireFrameAccurateCut)
        {
            arguments.AddRange(new[]
            {
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-c", "copy",
                "-avoid_negative_ts", "make_zero",
            });
        }
        else if (string.Equals(Path.GetExtension(request.OutputPath), ".webm", StringComparison.OrdinalIgnoreCase))
        {
            arguments.AddRange(new[]
            {
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-c:v", "libvpx-vp9",
                "-crf", "32",
                "-b:v", "0",
                "-c:a", "libopus",
            });
        }
        else
        {
            arguments.AddRange(new[]
            {
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-c:v", "libx264",
                "-preset", "veryfast",
                "-crf", "23",
                "-c:a", "aac",
                "-movflags", "+faststart",
            });
        }

        arguments.Add(request.OutputPath);
        return arguments;
    }

    private static bool HasMatchingContainer(string inputPath, string outputPath) =>
        string.Equals(
            Path.GetExtension(inputPath),
            Path.GetExtension(outputPath),
            StringComparison.OrdinalIgnoreCase);

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
}
