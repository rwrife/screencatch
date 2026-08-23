using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Export;

namespace ScreenCatch.Core.Tests;

public sealed class GifExportRequestTests
{
    [Fact]
    public void Constructor_RejectsRangePastSourceDuration()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            GifOutputFormat.Gif,
            range: new TrimRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4))));

        Assert.Equal("range", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveFrameRate()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            GifOutputFormat.Gif,
            framesPerSecond: 0));

        Assert.Equal("framesPerSecond", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveSourceDuration()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.Zero,
            1920,
            1080,
            GifOutputFormat.Gif));

        Assert.Equal("sourceDuration", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveWidth()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            GifOutputFormat.Gif,
            width: 0));

        Assert.Equal("width", error.ParamName);
    }

    [Theory]
    [InlineData(0, 1080, "sourceWidth")]
    [InlineData(1920, 0, "sourceHeight")]
    public void Constructor_RejectsNonPositiveSourceDimensions(
        int sourceWidth,
        int sourceHeight,
        string expectedParameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            sourceWidth,
            sourceHeight,
            GifOutputFormat.Gif));

        Assert.Equal(expectedParameter, error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsUndefinedOutputFormat()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            (GifOutputFormat)int.MaxValue));

        Assert.Equal("format", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsUndefinedDitheringMode()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            GifOutputFormat.Gif,
            dithering: (GifDithering)int.MaxValue));

        Assert.Equal("dithering", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsIdenticalInputAndOutputPaths()
    {
        var path = Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}.gif");
        File.WriteAllBytes(path, new byte[] { 1 });

        try
        {
            var error = Assert.Throws<ArgumentException>(() => new GifExportRequest(
                path,
                path,
                TimeSpan.FromSeconds(3),
                1920,
                1080,
                GifOutputFormat.Gif));

            Assert.Equal("outputPath", error.ParamName);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(GifOutputFormat.Gif, "clip.webp")]
    [InlineData(GifOutputFormat.AnimatedWebP, "clip.gif")]
    public void Constructor_RejectsOutputExtensionThatDoesNotMatchFormat(
        GifOutputFormat format,
        string outputPath)
    {
        var error = Assert.Throws<ArgumentException>(() => new GifExportRequest(
            "source.mp4",
            outputPath,
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            format));

        Assert.Equal("outputPath", error.ParamName);
    }

    [Fact]
    public void Constructor_RejectsCaseVariantOfInputPathOnCaseInsensitivePlatforms()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var inputPath = Path.Combine(Path.GetTempPath(), "ScreenCatch-Case-Input.gif");
        var outputPath = inputPath.ToUpperInvariant();

        var error = Assert.Throws<ArgumentException>(() => new GifExportRequest(
            inputPath,
            outputPath,
            TimeSpan.FromSeconds(3),
            1920,
            1080,
            GifOutputFormat.Gif));

        Assert.Equal("outputPath", error.ParamName);
    }
}
