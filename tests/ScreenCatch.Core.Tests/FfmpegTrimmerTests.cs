using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class FfmpegTrimmerTests
{
    [Fact]
    public async Task TrimAsync_RunsFfmpegAndReturnsCreatedOutput()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"trim-{Guid.NewGuid():N}.mp4");
        var engine = new OutputCreatingFfmpegEngine();
        ITrimmer trimmer = new FfmpegTrimmer(engine);

        try
        {
            var result = await trimmer.TrimAsync(new TrimRequest(
                "/captures/source.mp4",
                outputPath,
                new TrimRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3))));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(outputPath, result.OutputPath);
            Assert.Contains("copy", engine.Arguments);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task TrimAsync_ReturnsFailureWhenFfmpegFails()
    {
        var trimmer = new FfmpegTrimmer(new OutputCreatingFfmpegEngine(exitCode: 9, createOutputOnFailure: true));
        var outputPath = Path.Combine(Path.GetTempPath(), $"trim-{Guid.NewGuid():N}.mp4");

        var result = await trimmer.TrimAsync(new TrimRequest(
            "/captures/source.mp4",
            outputPath,
            new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(2))));

        Assert.False(result.IsSuccess);
        Assert.Contains("9", result.ErrorMessage);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task TrimAsync_CancellationRemovesPartialOutput()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"trim-{Guid.NewGuid():N}.mp4");
        var trimmer = new FfmpegTrimmer(new CancellingOutputCreatingEngine());
        var request = new TrimRequest(
            "/captures/source.mp4", outputPath, new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(2)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trimmer.TrimAsync(request));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void TrimRequest_RejectsIdenticalInputAndOutputPaths()
    {
        var path = Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, new byte[] { 1 });

        try
        {
            var error = Assert.Throws<ArgumentException>(() => new TrimRequest(
                path, path, new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(2))));

            Assert.Equal("outputPath", error.ParamName);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TrimRequest_RejectsCaseVariantOfInputPathOnCaseInsensitivePlatforms()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var inputPath = Path.Combine(Path.GetTempPath(), "ScreenCatch-Case-Input.mp4");
        var outputPath = inputPath.ToUpperInvariant();

        var error = Assert.Throws<ArgumentException>(() => new TrimRequest(
            inputPath,
            outputPath,
            new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(2))));

        Assert.Equal("outputPath", error.ParamName);
    }

    [Fact]
    public async Task TrimAsync_ReturnsFailureWhenOutputIsMissing()
    {
        var trimmer = new FfmpegTrimmer(new OutputCreatingFfmpegEngine(createOutput: false));
        var outputPath = Path.Combine(Path.GetTempPath(), $"trim-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(outputPath, new byte[] { 1 });

        var result = await trimmer.TrimAsync(new TrimRequest(
            "/captures/source.mp4",
            outputPath,
            new TrimRange(TimeSpan.Zero, TimeSpan.FromSeconds(2))));

        Assert.False(result.IsSuccess);
        Assert.Contains("no trimmed output", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    private sealed class OutputCreatingFfmpegEngine : IFfmpegEngine
    {
        private readonly int _exitCode;
        private readonly bool _createOutput;
        private readonly bool _createOutputOnFailure;

        public OutputCreatingFfmpegEngine(
            int exitCode = 0,
            bool createOutput = true,
            bool createOutputOnFailure = false)
        {
            _exitCode = exitCode;
            _createOutput = createOutput;
            _createOutputOnFailure = createOutputOnFailure;
        }

        public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();

        public Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default)
        {
            Arguments = arguments;
            if (_createOutput && (_exitCode == 0 || _createOutputOnFailure))
            {
                File.WriteAllBytes(arguments[^1], new byte[] { 1 });
            }

            return Task.FromResult(new FfmpegProcessResult(_exitCode, Array.Empty<string>()));
        }
    }

    private sealed class CancellingOutputCreatingEngine : IFfmpegEngine
    {
        public Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default)
        {
            File.WriteAllBytes(arguments[^1], new byte[] { 1 });
            return Task.FromCanceled<FfmpegProcessResult>(new CancellationToken(canceled: true));
        }
    }
}
