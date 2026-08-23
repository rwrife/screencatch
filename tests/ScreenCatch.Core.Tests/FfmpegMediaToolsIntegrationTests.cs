using System.Diagnostics;
using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Export;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class FfmpegMediaToolsIntegrationTests
{
    [Fact]
    public async Task ExportAsync_ProducesPlayableGifAndAnimatedWebPFromTrimmedSegment()
    {
        var sourcePath = TempPath(".mp4");
        var gifPath = TempPath(".gif");
        var webpPath = TempPath(".webp");

        try
        {
            CreateTestVideo(sourcePath);
            var exporter = new FfmpegGifExporter(new FfmpegProcessEngine());
            var range = new TrimRange(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1250));

            var gifResult = await exporter.ExportAsync(new GifExportRequest(
                sourcePath,
                gifPath,
                TimeSpan.FromSeconds(2),
                96,
                64,
                GifOutputFormat.Gif,
                framesPerSecond: 8,
                width: 64,
                range: range));
            var webpResult = await exporter.ExportAsync(new GifExportRequest(
                sourcePath,
                webpPath,
                TimeSpan.FromSeconds(2),
                96,
                64,
                GifOutputFormat.AnimatedWebP,
                framesPerSecond: 8,
                width: 64,
                range: range));

            Assert.True(gifResult.IsSuccess, gifResult.ErrorMessage);
            Assert.True(webpResult.IsSuccess, webpResult.ErrorMessage);
            Assert.Contains("codec_name=gif", ProbeVideo(gifPath), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("codec_name=webp", ProbeVideo(webpPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(sourcePath, gifPath, webpPath);
        }
    }

    [Fact]
    public async Task TrimAsync_ProducesPlayableStreamCopyClip()
    {
        var sourcePath = TempPath(".mp4");
        var outputPath = TempPath(".mp4");

        try
        {
            CreateTestVideo(sourcePath);
            var trimmer = new FfmpegTrimmer(new FfmpegProcessEngine());

            var result = await trimmer.TrimAsync(new TrimRequest(
                sourcePath,
                outputPath,
                new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(1))));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains("codec_name=h264", ProbeVideo(outputPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(sourcePath, outputPath);
        }
    }

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"screencatch-media-{Guid.NewGuid():N}{extension}");

    private static void CreateTestVideo(string outputPath)
    {
        RunProcess(
            "ffmpeg",
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi",
            "-i", "testsrc=size=96x64:rate=12",
            "-t", "2",
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-g", "12",
            outputPath);
    }

    private static string ProbeVideo(string path) => RunProcess(
        "ffprobe",
        "-v", "error",
        "-select_streams", "v:0",
        "-show_entries", "stream=codec_name,width,height,duration",
        "-of", "default=noprint_wrappers=1",
        path);

    private static string RunProcess(string fileName, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    private static void SafeDelete(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Cleanup best-effort only.
            }
        }
    }
}
