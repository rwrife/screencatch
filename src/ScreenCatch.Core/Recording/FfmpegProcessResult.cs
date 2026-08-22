namespace ScreenCatch.Core.Recording;

public sealed class FfmpegProcessResult
{
    public FfmpegProcessResult(int exitCode, IReadOnlyList<string> standardErrorLines)
    {
        ExitCode = exitCode;
        StandardErrorLines = standardErrorLines ?? Array.Empty<string>();
    }

    public int ExitCode { get; }

    public IReadOnlyList<string> StandardErrorLines { get; }
}
