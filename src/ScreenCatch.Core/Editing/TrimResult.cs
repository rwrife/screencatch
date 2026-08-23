namespace ScreenCatch.Core.Editing;

public sealed class TrimResult
{
    private TrimResult(bool isSuccess, string? outputPath, string? errorMessage)
    {
        IsSuccess = isSuccess;
        OutputPath = outputPath;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? OutputPath { get; }

    public string? ErrorMessage { get; }

    public static TrimResult Success(string outputPath) => new(true, outputPath, null);

    public static TrimResult Failure(string errorMessage) => new(false, null, errorMessage);
}
