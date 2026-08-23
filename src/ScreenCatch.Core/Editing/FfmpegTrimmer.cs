using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Editing;

public sealed class FfmpegTrimmer : ITrimmer
{
    private readonly IFfmpegEngine _ffmpegEngine;

    public FfmpegTrimmer(IFfmpegEngine ffmpegEngine)
    {
        _ffmpegEngine = ffmpegEngine ?? throw new ArgumentNullException(nameof(ffmpegEngine));
    }

    public async Task<TrimResult> TrimAsync(TrimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        TryDelete(request.OutputPath);
        var succeeded = false;
        try
        {
            var result = await _ffmpegEngine.RunAsync(
                TrimArgumentBuilder.Build(request),
                static (_, _) => Task.CompletedTask,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                return TrimResult.Failure($"ffmpeg exited with code {result.ExitCode}.");
            }

            var output = new FileInfo(request.OutputPath);
            if (!output.Exists || output.Length == 0)
            {
                return TrimResult.Failure("ffmpeg completed but no trimmed output was produced.");
            }

            succeeded = true;
            return TrimResult.Success(request.OutputPath);
        }
        finally
        {
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
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
