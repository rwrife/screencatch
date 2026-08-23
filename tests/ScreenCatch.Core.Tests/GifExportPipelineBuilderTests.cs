using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Export;

namespace ScreenCatch.Core.Tests;

public sealed class GifExportPipelineBuilderTests
{
    [Fact]
    public void Build_Gif_UsesTwoPassPaletteWithTrimAndControls()
    {
        var request = new GifExportRequest(
            "/captures/source.mp4",
            "/captures/clip.gif",
            TimeSpan.FromSeconds(12),
            1920,
            1080,
            GifOutputFormat.Gif,
            framesPerSecond: 12,
            width: 640,
            dithering: GifDithering.Sierra2_4A,
            range: new TrimRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)));

        var pipeline = GifExportPipelineBuilder.Build(request, "/tmp/palette.png");
        var paletteFilter = ValueAfter(pipeline.PaletteArguments, "-vf");
        var exportFilter = ValueAfter(pipeline.ExportArguments, "-lavfi");

        Assert.Contains("fps=12", paletteFilter);
        Assert.Contains("scale=640:-2:flags=lanczos", paletteFilter);
        Assert.Contains("palettegen=stats_mode=diff", paletteFilter);
        Assert.Contains("-frames:v", pipeline.PaletteArguments);
        Assert.Contains("1", pipeline.PaletteArguments);
        Assert.Contains("paletteuse=dither=sierra2_4a", exportFilter);
        Assert.Contains("/tmp/palette.png", pipeline.PaletteArguments);
        Assert.Contains("/tmp/palette.png", pipeline.ExportArguments);
        Assert.Contains("2", pipeline.PaletteArguments);
        Assert.Contains("6", pipeline.PaletteArguments);
        Assert.Equal("/captures/clip.gif", pipeline.ExportArguments[^1]);
    }

    [Fact]
    public void Build_AnimatedWebP_UsesPaletteAndWebPEncoder()
    {
        var request = new GifExportRequest(
            "/captures/source.webm",
            "/captures/clip.webp",
            TimeSpan.FromSeconds(5),
            1920,
            1080,
            GifOutputFormat.AnimatedWebP,
            framesPerSecond: 10,
            width: 480,
            dithering: GifDithering.Bayer);

        var pipeline = GifExportPipelineBuilder.Build(request, "/tmp/palette.png");

        Assert.Contains("palettegen=stats_mode=diff", ValueAfter(pipeline.PaletteArguments, "-vf"));
        Assert.Contains("paletteuse=dither=bayer", ValueAfter(pipeline.ExportArguments, "-lavfi"));
        Assert.Contains("libwebp_anim", pipeline.ExportArguments);
        Assert.Contains("-lossless", pipeline.ExportArguments);
        Assert.Contains("-q:v", pipeline.ExportArguments);
        Assert.Equal("/captures/clip.webp", pipeline.ExportArguments[^1]);
    }

    private static string ValueAfter(IReadOnlyList<string> arguments, string option)
    {
        var index = arguments.ToList().IndexOf(option);
        Assert.True(index >= 0, $"Missing option {option}.");
        return arguments[index + 1];
    }
}
