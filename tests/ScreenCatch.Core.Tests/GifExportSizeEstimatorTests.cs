using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Export;

namespace ScreenCatch.Core.Tests;

public sealed class GifExportSizeEstimatorTests
{
    [Fact]
    public void EstimateBytes_IncreasesWithFrameRateAndRespectsTrimmedDuration()
    {
        var lowFps = new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(20),
            1920,
            1080,
            GifOutputFormat.Gif,
            framesPerSecond: 10,
            width: 640,
            range: new TrimRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));
        var highFps = new GifExportRequest(
            "source.mp4",
            "clip.gif",
            TimeSpan.FromSeconds(20),
            1920,
            1080,
            GifOutputFormat.Gif,
            framesPerSecond: 20,
            width: 640,
            range: new TrimRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));

        var lowEstimate = GifExportSizeEstimator.EstimateBytes(lowFps);
        var highEstimate = GifExportSizeEstimator.EstimateBytes(highFps);

        Assert.True(lowEstimate > 0);
        Assert.True(highEstimate > lowEstimate);
        Assert.InRange(highEstimate / (double)lowEstimate, 1.9, 2.1);
    }

    [Fact]
    public void EstimateBytes_UsesSourceAspectRatioForScaledHeight()
    {
        var landscape = new GifExportRequest(
            "source.mp4", "clip.gif", TimeSpan.FromSeconds(1),
            1600, 900, GifOutputFormat.Gif, framesPerSecond: 1, width: 800);
        var square = new GifExportRequest(
            "source.mp4", "clip.gif", TimeSpan.FromSeconds(1),
            1000, 1000, GifOutputFormat.Gif, framesPerSecond: 1, width: 800);

        var landscapeEstimate = GifExportSizeEstimator.EstimateBytes(landscape);
        var squareEstimate = GifExportSizeEstimator.EstimateBytes(square);

        Assert.Equal(54_000, landscapeEstimate);
        Assert.Equal(96_000, squareEstimate);
    }
}
