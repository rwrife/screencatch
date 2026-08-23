namespace ScreenCatch.Core.Export;

public sealed class GifExportResult
{
    private GifExportResult(bool isSuccess, string? outputPath, string? errorMessage)
    {
        IsSuccess = isSuccess;
        OutputPath = outputPath;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? OutputPath { get; }

    public string? ErrorMessage { get; }

    public static GifExportResult Success(string outputPath) => new(true, outputPath, null);

    public static GifExportResult Failure(string errorMessage) => new(false, null, errorMessage);
}
