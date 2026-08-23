using ScreenCatch.Core.Export;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class FfmpegGifExporterTests
{
    [Fact]
    public async Task ExportAsync_RunsPaletteThenExportAndCleansTemporaryPalette()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.gif");
        var engine = new OutputCreatingEngine();
        IGifExporter exporter = new FfmpegGifExporter(engine);
        var request = new GifExportRequest(
            "/captures/source.mp4",
            outputPath,
            TimeSpan.FromSeconds(4),
            1920,
            1080,
            GifOutputFormat.Gif,
            framesPerSecond: 12,
            width: 320);

        try
        {
            var estimate = exporter.EstimateOutputSize(request);
            var result = await exporter.ExportAsync(request);

            Assert.True(estimate > 0);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(2, engine.Calls.Count);
            Assert.Contains("palettegen", engine.Calls[0].Single(value => value.Contains("palettegen", StringComparison.Ordinal)));
            Assert.Contains("paletteuse", engine.Calls[1].Single(value => value.Contains("paletteuse", StringComparison.Ordinal)));
            Assert.False(File.Exists(engine.Calls[0][^1]));
            Assert.Equal(outputPath, result.OutputPath);
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
    public async Task ExportAsync_StopsWhenPalettePassFails()
    {
        var engine = new OutputCreatingEngine(7);
        var exporter = new FfmpegGifExporter(engine);
        var request = new GifExportRequest(
            "source.mp4",
            Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.gif"),
            TimeSpan.FromSeconds(2),
            1920,
            1080,
            GifOutputFormat.Gif);

        var result = await exporter.ExportAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("palette", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(engine.Calls);
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailureWhenExportPassFails()
    {
        var engine = new OutputCreatingEngine(createOutput: true, createOutputOnFailure: true, 0, 4);
        var exporter = new FfmpegGifExporter(engine);
        var outputPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.webp");
        var request = new GifExportRequest(
            "source.mp4",
            outputPath,
            TimeSpan.FromSeconds(2),
            1920,
            1080,
            GifOutputFormat.AnimatedWebP);

        var result = await exporter.ExportAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("export", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, engine.Calls.Count);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_CancellationRemovesPartialOutput()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.gif");
        var exporter = new FfmpegGifExporter(new CancellingOutputCreatingEngine());
        var request = new GifExportRequest(
            "source.mp4", outputPath, TimeSpan.FromSeconds(2), 1920, 1080, GifOutputFormat.Gif);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exporter.ExportAsync(request));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailureWhenOutputIsMissing()
    {
        var engine = new OutputCreatingEngine(createOutput: false);
        var exporter = new FfmpegGifExporter(engine);
        var outputPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.gif");
        File.WriteAllBytes(outputPath, new byte[] { 1 });
        var request = new GifExportRequest(
            "source.mp4",
            outputPath,
            TimeSpan.FromSeconds(2),
            1920,
            1080,
            GifOutputFormat.Gif);

        var result = await exporter.ExportAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("no exported output", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    private sealed class OutputCreatingEngine : IFfmpegEngine
    {
        private readonly Queue<int> _exitCodes;
        private readonly bool _createOutput;
        private readonly bool _createOutputOnFailure;

        public OutputCreatingEngine(params int[] exitCodes)
            : this(true, false, exitCodes)
        {
        }

        public OutputCreatingEngine(bool createOutput, bool createOutputOnFailure = false, params int[] exitCodes)
        {
            _createOutput = createOutput;
            _createOutputOnFailure = createOutputOnFailure;
            _exitCodes = new Queue<int>(exitCodes);
        }

        public List<IReadOnlyList<string>> Calls { get; } = new();

        public Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(arguments);
            var exitCode = _exitCodes.Count > 0 ? _exitCodes.Dequeue() : 0;
            if (_createOutput && (exitCode == 0 || _createOutputOnFailure))
            {
                File.WriteAllBytes(arguments[^1], new byte[] { 1 });
            }

            return Task.FromResult(new FfmpegProcessResult(exitCode, Array.Empty<string>()));
        }
    }

    private sealed class CancellingOutputCreatingEngine : IFfmpegEngine
    {
        private int _callCount;

        public Task<FfmpegProcessResult> RunAsync(
            IReadOnlyList<string> arguments,
            Func<Stream, CancellationToken, Task> writeStandardInputAsync,
            Action<string>? onStandardErrorLine = null,
            CancellationToken cancellationToken = default)
        {
            if (_callCount++ == 0)
            {
                return Task.FromResult(new FfmpegProcessResult(0, Array.Empty<string>()));
            }

            File.WriteAllBytes(arguments[^1], new byte[] { 1 });
            return Task.FromCanceled<FfmpegProcessResult>(new CancellationToken(canceled: true));
        }
    }
}
