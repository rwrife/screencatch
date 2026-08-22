using System.Diagnostics;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class FfmpegVideoEncoderIntegrationTests
{
    [Fact]
    public async Task EncodeAsync_ProducesPlayableMp4_FromSyntheticFrames()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"screencatch-{Guid.NewGuid():N}.mp4");

        try
        {
            var request = new VideoEncodeRequest(
                frames: CreateSyntheticFrames(frameCount: 18, width: 64, height: 36),
                options: new VideoEncodeOptions(outputPath, VideoOutputFormat.Mp4, framesPerSecond: 12, constantRateFactor: 24));

            var encoder = new FfmpegVideoEncoder(new FfmpegProcessEngine());
            var result = await encoder.EncodeAsync(request);

            Assert.True(result.IsSuccess, result.Error?.Message ?? "expected success");
            Assert.True(File.Exists(outputPath));

            var probe = RunFfprobe(outputPath);
            Assert.Contains("codec_name=h264", probe, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("width=64", probe, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("height=36", probe, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(outputPath);
        }
    }

    [Fact]
    public async Task EncodeAsync_ProducesPlayableWebm_FromSyntheticFrames()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"screencatch-{Guid.NewGuid():N}.webm");

        try
        {
            var request = new VideoEncodeRequest(
                frames: CreateSyntheticFrames(frameCount: 14, width: 80, height: 48),
                options: new VideoEncodeOptions(outputPath, VideoOutputFormat.WebM, framesPerSecond: 10, constantRateFactor: 34));

            var encoder = new FfmpegVideoEncoder(new FfmpegProcessEngine());
            var result = await encoder.EncodeAsync(request);

            Assert.True(result.IsSuccess, result.Error?.Message ?? "expected success");
            Assert.True(File.Exists(outputPath));

            var probe = RunFfprobe(outputPath);
            Assert.Contains("codec_name=vp9", probe, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("width=80", probe, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("height=48", probe, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(outputPath);
        }
    }

    private static IReadOnlyList<CaptureFrame> CreateSyntheticFrames(int frameCount, int width, int height)
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var provider = new SyntheticFrameProvider(topology);
        var frames = new List<CaptureFrame>(capacity: frameCount);
        var cropRect = new CaptureRect(0, 0, width, height);

        for (var i = 0; i < frameCount; i++)
        {
            var fullFrame = provider.CaptureFrame(i, DateTimeOffset.UtcNow);
            frames.Add(FrameCropper.CropFrame(fullFrame, cropRect));
        }

        return frames;
    }

    private static string RunFfprobe(string outputPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add("error");
        process.StartInfo.ArgumentList.Add("-select_streams");
        process.StartInfo.ArgumentList.Add("v:0");
        process.StartInfo.ArgumentList.Add("-show_entries");
        process.StartInfo.ArgumentList.Add("stream=codec_name,width,height");
        process.StartInfo.ArgumentList.Add("-of");
        process.StartInfo.ArgumentList.Add("default=noprint_wrappers=1");
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed with code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    private static void SafeDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // cleanup best-effort only
        }
    }
}
