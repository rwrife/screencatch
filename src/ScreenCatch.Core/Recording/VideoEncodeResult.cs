namespace ScreenCatch.Core.Recording;

public sealed class VideoEncodeResult
{
    private VideoEncodeResult(
        bool isSuccess,
        string? outputPath,
        VideoEncodeError? error,
        int? exitCode,
        IReadOnlyList<string> diagnostics)
    {
        IsSuccess = isSuccess;
        OutputPath = outputPath;
        Error = error;
        ExitCode = exitCode;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess { get; }

    public string? OutputPath { get; }

    public VideoEncodeError? Error { get; }

    public int? ExitCode { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public static VideoEncodeResult Success(string outputPath, IReadOnlyList<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        return new VideoEncodeResult(true, outputPath, null, 0, diagnostics ?? Array.Empty<string>());
    }

    public static VideoEncodeResult Failure(
        VideoEncodeError error,
        int? exitCode = null,
        IReadOnlyList<string>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new VideoEncodeResult(false, null, error, exitCode, diagnostics ?? Array.Empty<string>());
    }
}
