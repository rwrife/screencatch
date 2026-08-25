using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Cli.Tests;

public sealed class CliValueParserTests
{
    [Fact]
    public void ParseRect_RequiresFourIntegerCoordinates()
    {
        Assert.Equal(new CaptureRect(-10, 20, 640, 360), CliValueParser.ParseRect("-10,20,640,360"));
        Assert.Throws<CliUsageException>(() => CliValueParser.ParseRect("10,20,640"));
    }

    [Fact]
    public void BuildPreset_OverlaysCommandLineValuesOnSavedPreset()
    {
        var saved = new RecordingPreset(
            "demo",
            CaptureSourceKind.Window,
            FramesPerSecond: 30,
            Format: VideoOutputFormat.Mp4,
            Quality: 23,
            WindowTitle: "Terminal");
        var invocation = CliParser.Parse([
            "record", "--preset", "demo", "--fps", "15", "--format", "webm", "--out", "demo.webm",
        ]);

        var merged = CliValueParser.BuildPreset(invocation, saved);

        Assert.Equal(CaptureSourceKind.Window, merged.Source);
        Assert.Equal("Terminal", merged.WindowTitle);
        Assert.Equal(15, merged.FramesPerSecond);
        Assert.Equal(VideoOutputFormat.WebM, merged.Format);
        Assert.Equal(23, merged.Quality);
    }

    [Theory]
    [InlineData("2.5", 2.5)]
    [InlineData("00:00:02.500", 2.5)]
    public void ParseTime_AcceptsSecondsAndClockValues(string value, double expectedSeconds)
    {
        Assert.Equal(expectedSeconds, CliValueParser.ParseTime(value, "start").TotalSeconds, precision: 3);
    }

    [Fact]
    public void GetMaxFrames_LeavesCtrlCRecordingUnbounded()
    {
        Assert.Equal(int.MaxValue, CliRecordingLimits.GetMaxFrames(duration: null, framesPerSecond: 30));
        Assert.Equal(15, CliRecordingLimits.GetMaxFrames(TimeSpan.FromSeconds(0.5), framesPerSecond: 30));
    }
}
