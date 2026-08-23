namespace ScreenCatch.Core.Editing;

public sealed class TrimRequest
{
    public TrimRequest(
        string inputPath,
        string outputPath,
        TrimRange range,
        bool requireFrameAccurateCut = false)
    {
        if (PathsReferToSameFile(inputPath, outputPath))
        {
            throw new ArgumentException("The output path must differ from the input path.", nameof(outputPath));
        }

        InputPath = inputPath;
        OutputPath = outputPath;
        Range = range;
        RequireFrameAccurateCut = requireFrameAccurateCut;
    }

    public string InputPath { get; }

    public string OutputPath { get; }

    public TrimRange Range { get; }

    public bool RequireFrameAccurateCut { get; }

    private static bool PathsReferToSameFile(string inputPath, string outputPath) =>
        string.Equals(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
