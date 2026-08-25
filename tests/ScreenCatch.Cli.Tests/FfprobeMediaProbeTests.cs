namespace ScreenCatch.Cli.Tests;

public sealed class FfprobeMediaProbeTests
{
    [Fact]
    public void Parse_ReadsDurationAndVideoDimensions()
    {
        const string json = """
            {"format":{"duration":"2.500000"},"streams":[{"codec_type":"audio"},{"codec_type":"video","width":640,"height":360}]}
            """;

        var info = FfprobeMediaProbe.Parse(json);

        Assert.Equal(2.5, info.Duration.TotalSeconds, precision: 3);
        Assert.Equal(640, info.Width);
        Assert.Equal(360, info.Height);
    }
}
