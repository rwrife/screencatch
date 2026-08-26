using System.ComponentModel;
using ScreenCatch.Core.Ai;
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

    [Fact]
    public async Task RunAsync_RecordWithAiOptIn_ReportsLocalSuggestionInJson()
    {
        var output = new StringWriter();
        var aiService = new FakeAiService();
        var topology = CaptureTopology.CreateDefaultForTests();
        var outputPath = Path.Combine(_directory, "demo.mp4");
        var application = new CliApplication(
            output,
            new JsonPresetStore(_directory),
            new SuccessfulFfmpegEngine(outputPath),
            captureSourceFactory: () => new SyntheticScreenCaptureSource(new SyntheticFrameProvider(topology)),
            aiService: aiService);
        var invocation = CliParser.Parse([
            "record", "--source", "region", "--rect", "0,0,1,1", "--fps", "5",
            "--duration", "0.01", "--out", outputPath, "--ai",
            "--ai-endpoint", "http://localhost:1234/v1/", "--ai-model", "tiny-local", "--json",
        ]);

        var exitCode = await application.RunAsync(invocation);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.NotNull(aiService.Options);
        Assert.True(aiService.Options!.Enabled);
        Assert.Equal("http://localhost:1234/v1/", aiService.Options.Endpoint);
        Assert.Equal("tiny-local", aiService.Options.Model);
        Assert.Contains("\"title\":\"Local title\"", output.ToString());
        Assert.Contains("\"caption\":\"Local caption\"", output.ToString());
    }

    [Fact]
    public async Task RunAsync_RecordWithoutAiFlag_DoesNotCallAiService()
    {
        var aiService = new FakeAiService();
        var topology = CaptureTopology.CreateDefaultForTests();
        var outputPath = Path.Combine(_directory, "without-ai.mp4");
        using var application = new CliApplication(
            new StringWriter(),
            new JsonPresetStore(_directory),
            new SuccessfulFfmpegEngine(outputPath),
            captureSourceFactory: () => new SyntheticScreenCaptureSource(new SyntheticFrameProvider(topology)),
            aiService: aiService);
        var invocation = CliParser.Parse([
            "record", "--source", "region", "--rect", "0,0,1,1", "--fps", "5",
            "--duration", "0.01", "--out", outputPath,
        ]);

        var exitCode = await application.RunAsync(invocation);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Null(aiService.Options);
    }

    [Fact]
    public void Dispose_DoesNotDisposeInjectedAiService()
    {
        var aiService = new FakeAiService();
        var application = new CliApplication(new StringWriter(), aiService: aiService);

        application.Dispose();

        Assert.False(aiService.IsDisposed);
    }

    private sealed class FakeAiService : IRecordingAiService, IDisposable
    {
        public RecordingAiOptions? Options { get; private set; }

        public bool IsDisposed { get; private set; }

        public Task<bool> ProbeAsync(RecordingAiOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<RecordingAiSuggestion> SuggestAsync(
            RecordingAiRequest request,
            RecordingAiOptions options,
            CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new RecordingAiSuggestion("Local title", "Local caption", IsFallback: false));
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class SuccessfulFfmpegEngine(string outputPath) : IFfmpegEngine
    {
        public async Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default)
        {
            await writeStandardInputAsync(Stream.Null, cancellationToken);
            await File.WriteAllBytesAsync(outputPath, [1], cancellationToken);
            return new FfmpegProcessResult(0, []);
        }
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
