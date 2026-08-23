using System.Globalization;

namespace ScreenCatch.Core.Export;

public static class GifExportPipelineBuilder
{
    public static GifExportPipeline Build(GifExportRequest request, string palettePath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var range = request.EffectiveRange;
        var transform = $"fps={request.FramesPerSecond.ToString(CultureInfo.InvariantCulture)},scale={request.Width.ToString(CultureInfo.InvariantCulture)}:-2:flags=lanczos";
        var start = FormatSeconds(range.Start);
        var duration = FormatSeconds(range.Duration);

        var paletteArguments = new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-ss", start,
            "-i", request.InputPath,
            "-t", duration,
            "-vf", $"{transform},palettegen=stats_mode=diff",
            "-frames:v", "1",
            palettePath,
        };

        var exportArguments = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-ss", start,
            "-i", request.InputPath,
            "-i", palettePath,
            "-t", duration,
            "-lavfi", $"[0:v]{transform}[x];[x][1:v]paletteuse=dither={FormatDithering(request.Dithering)}:diff_mode=rectangle",
            "-loop", "0",
        };

        if (request.Format == GifOutputFormat.AnimatedWebP)
        {
            exportArguments.AddRange(new[]
            {
                "-c:v", "libwebp_anim",
                "-lossless", "0",
                "-compression_level", "6",
                "-q:v", "75",
            });
        }

        exportArguments.Add(request.OutputPath);
        return new GifExportPipeline(paletteArguments, exportArguments);
    }

    private static string FormatDithering(GifDithering dithering) => dithering switch
    {
        GifDithering.None => "none",
        GifDithering.Bayer => "bayer",
        GifDithering.FloydSteinberg => "floyd_steinberg",
        GifDithering.Sierra2 => "sierra2",
        GifDithering.Sierra2_4A => "sierra2_4a",
        _ => throw new ArgumentOutOfRangeException(nameof(dithering), dithering, "Unsupported dithering mode."),
    };

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
}
