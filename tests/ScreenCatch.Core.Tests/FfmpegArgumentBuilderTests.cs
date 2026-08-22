using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class FfmpegArgumentBuilderTests
{
    [Fact]
    public void Build_Mp4WithCrf_UsesArrayArgumentsAndNoAudio()
    {
        var request = new VideoEncodeRequest(
            frames: CreateFrames(count: 3, width: 4, height: 3),
            options: new VideoEncodeOptions(
                outputPath: "/tmp/out with spaces.mp4",
                format: VideoOutputFormat.Mp4,
                framesPerSecond: 15,
                constantRateFactor: 21));

        var args = FfmpegArgumentBuilder.Build(request).ToArray();

        Assert.Contains("-f", args);
        Assert.Contains("rawvideo", args);
        Assert.Contains("-i", args);
        Assert.Contains("pipe:0", args);
        Assert.Contains("-c:v", args);
        Assert.Contains("libx264", args);
        Assert.Contains("-crf", args);
        Assert.Contains("21", args);
        Assert.Contains("-an", args);

        Assert.Equal("/tmp/out with spaces.mp4", args[^1]);
        Assert.DoesNotContain(args, a => a.Contains("ffmpeg ", StringComparison.Ordinal));
        Assert.DoesNotContain(args, a => a.Contains("|", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WebmWithBitrateAndAudio_MapsAudioTrack()
    {
        var request = new VideoEncodeRequest(
            frames: CreateFrames(count: 4, width: 8, height: 6),
            options: new VideoEncodeOptions(
                outputPath: "/tmp/out.webm",
                format: VideoOutputFormat.WebM,
                framesPerSecond: 24,
                targetBitrate: "2M"),
            audioTrack: new AudioCaptureResult("/tmp/audio track.wav"));

        var args = FfmpegArgumentBuilder.Build(request).ToArray();

        Assert.Contains("-c:v", args);
        Assert.Contains("libvpx-vp9", args);
        Assert.Contains("-b:v", args);
        Assert.Contains("2M", args);
        Assert.Contains("-map", args);
        Assert.Contains("1:a:0", args);
        Assert.Contains("-c:a", args);
        Assert.Contains("libopus", args);
        Assert.Contains("/tmp/audio track.wav", args);
        Assert.Equal("/tmp/out.webm", args[^1]);
    }

    private static IReadOnlyList<CaptureFrame> CreateFrames(int count, int width, int height)
    {
        var frames = new List<CaptureFrame>(capacity: count);
        var stride = width * 4;

        for (var i = 0; i < count; i++)
        {
            var buffer = new byte[stride * height];
            frames.Add(new CaptureFrame(DateTimeOffset.UtcNow, new CaptureRect(0, 0, width, height), stride, buffer));
        }

        return frames;
    }
}
