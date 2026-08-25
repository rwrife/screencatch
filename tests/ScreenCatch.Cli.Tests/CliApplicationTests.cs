using System.ComponentModel;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"screencatch-cli-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAsync_PresetSave_WritesSharedPresetAndJsonResult()
    {
        var output = new StringWriter();
        var store = new JsonPresetStore(_directory);
        var application = new CliApplication(output, store);
        var invocation = CliParser.Parse([
            "preset", "save", "demo", "--source", "region", "--rect", "1,2,320,200", "--fps", "12", "--json",
        ]);

        var exitCode = await application.RunAsync(invocation);
        var saved = await store.LoadAsync("demo");

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.NotNull(saved);
        Assert.Equal(12, saved!.FramesPerSecond);
        Assert.Contains("\"success\":true", output.ToString());
        Assert.Contains("\"command\":\"preset save\"", output.ToString());
    }

    [Fact]
    public async Task RunAsync_Record_ReturnsToolUnavailableWhenFfmpegCannotStart()
    {
        var output = new StringWriter();
        var topology = CaptureTopology.CreateDefaultForTests();
        var application = new CliApplication(
            output,
            new JsonPresetStore(_directory),
            new MissingFfmpegEngine(),
            captureSourceFactory: () => new SyntheticScreenCaptureSource(new SyntheticFrameProvider(topology)));
        var invocation = CliParser.Parse([
            "record", "--source", "region", "--rect", "0,0,1,1", "--fps", "5",
            "--duration", "0.01", "--out", Path.Combine(_directory, "demo.mp4"), "--json",
        ]);

        var exitCode = await application.RunAsync(invocation);

        Assert.Equal(CliExitCode.ToolUnavailable, exitCode);
        Assert.Contains("\"exitCode\":4", output.ToString());
    }

    private sealed class MissingFfmpegEngine : IFfmpegEngine
    {
        public Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default) =>
            throw new Win32Exception("ffmpeg was not found");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
