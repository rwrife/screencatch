using ScreenCatch.Core.Editing;

namespace ScreenCatch.Core.Export;

public sealed class GifExportRequest
{
    public GifExportRequest(
        string inputPath,
        string outputPath,
        TimeSpan sourceDuration,
        int sourceWidth,
        int sourceHeight,
        GifOutputFormat format,
        int framesPerSecond = 15,
        int width = 960,
        GifDithering dithering = GifDithering.Sierra2_4A,
        TrimRange? range = null)
    {
        if (sourceDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceDuration), "Source duration must be positive.");
        }

        if (sourceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source width must be positive.");
        }

        if (sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceHeight), "Source height must be positive.");
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.");
        }

        if (!Enum.IsDefined(dithering))
        {
            throw new ArgumentOutOfRangeException(nameof(dithering), dithering, "Unsupported dithering mode.");
        }

        if (framesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "Frame rate must be positive.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Output width must be positive.");
        }

        if (range is not null && range.Value.End > sourceDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The trim range cannot extend past the source duration.");
        }

        var expectedExtension = format == GifOutputFormat.Gif ? ".gif" : ".webp";
        if (!string.Equals(Path.GetExtension(outputPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The output path must use the {expectedExtension} extension for {format} output.",
                nameof(outputPath));
        }

        if (PathsReferToSameFile(inputPath, outputPath))
        {
            throw new ArgumentException("The output path must differ from the input path.", nameof(outputPath));
        }

        InputPath = inputPath;
        OutputPath = outputPath;
        SourceDuration = sourceDuration;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        Format = format;
        FramesPerSecond = framesPerSecond;
        Width = width;
        Dithering = dithering;
        Range = range;
    }

    public string InputPath { get; }

    public string OutputPath { get; }

    public TimeSpan SourceDuration { get; }

    public int SourceWidth { get; }

    public int SourceHeight { get; }

    public GifOutputFormat Format { get; }

    public int FramesPerSecond { get; }

    public int Width { get; }

    public GifDithering Dithering { get; }

    public TrimRange? Range { get; }

    public TrimRange EffectiveRange => Range ?? new TrimRange(TimeSpan.Zero, SourceDuration);

    private static bool PathsReferToSameFile(string inputPath, string outputPath) =>
        string.Equals(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
