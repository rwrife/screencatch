using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Export;

public sealed class FfmpegGifExporter : IGifExporter
{
    private readonly IFfmpegEngine _ffmpegEngine;

    public FfmpegGifExporter(IFfmpegEngine ffmpegEngine)
    {
        _ffmpegEngine = ffmpegEngine ?? throw new ArgumentNullException(nameof(ffmpegEngine));
    }

    public long EstimateOutputSize(GifExportRequest request) => GifExportSizeEstimator.EstimateBytes(request);

    public async Task<GifExportResult> ExportAsync(
        GifExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var palettePath = Path.Combine(
            Path.GetTempPath(),
            $"screencatch-palette-{Guid.NewGuid():N}.png");

        TryDelete(request.OutputPath);
        var succeeded = false;
        try
        {
            var pipeline = GifExportPipelineBuilder.Build(request, palettePath);
            var paletteResult = await _ffmpegEngine.RunAsync(
                pipeline.PaletteArguments,
                static (_, _) => Task.CompletedTask,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (paletteResult.ExitCode != 0)
            {
                return GifExportResult.Failure(
                    $"The ffmpeg palette pass exited with code {paletteResult.ExitCode}.");
            }

            var exportResult = await _ffmpegEngine.RunAsync(
                pipeline.ExportArguments,
                static (_, _) => Task.CompletedTask,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (exportResult.ExitCode != 0)
            {
                return GifExportResult.Failure(
                    $"The ffmpeg export pass exited with code {exportResult.ExitCode}.");
            }

            var output = new FileInfo(request.OutputPath);
            if (!output.Exists || output.Length == 0)
            {
                return GifExportResult.Failure("ffmpeg completed but no exported output was produced.");
            }

            succeeded = true;
            return GifExportResult.Success(request.OutputPath);
        }
        finally
        {
            TryDelete(palettePath);
            if (!succeeded)
            {
                TryDelete(request.OutputPath);
            }
        }
    }

    private static void TryDelete(string path)
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
            // Best-effort cleanup only.
        }
    }
}
