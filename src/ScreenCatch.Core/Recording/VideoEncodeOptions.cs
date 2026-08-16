using System.Globalization;

namespace ScreenCatch.Core.Recording;

public sealed class VideoEncodeOptions
{
    public VideoEncodeOptions(
        string outputPath,
        VideoOutputFormat format,
        int framesPerSecond,
        int? constantRateFactor = null,
        string? targetBitrate = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        if (framesPerSecond is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "FPS must be between 1 and 120.");
        }

        if (constantRateFactor is not null && !string.IsNullOrWhiteSpace(targetBitrate))
        {
            throw new ArgumentException("Choose either CRF or bitrate, not both.", nameof(targetBitrate));
        }

        if (constantRateFactor is < 0 or > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(constantRateFactor), "CRF must be between 0 and 63.");
        }

        if (targetBitrate is not null)
        {
            targetBitrate = targetBitrate.Trim();
            if (targetBitrate.Length == 0)
            {
                throw new ArgumentException("Bitrate cannot be empty whitespace.", nameof(targetBitrate));
            }
        }

        OutputPath = outputPath;
        Format = format;
        FramesPerSecond = framesPerSecond;
        ConstantRateFactor = constantRateFactor;
        TargetBitrate = targetBitrate;
    }

    public string OutputPath { get; }

    public VideoOutputFormat Format { get; }

    public int FramesPerSecond { get; }

    public int? ConstantRateFactor { get; }

    public string? TargetBitrate { get; }

    public override string ToString()
    {
        var quality = ConstantRateFactor is not null
            ? $"crf={ConstantRateFactor.Value.ToString(CultureInfo.InvariantCulture)}"
            : TargetBitrate is not null
                ? $"bitrate={TargetBitrate}"
                : "quality=default";

        return $"{Format} {FramesPerSecond}fps {quality} -> {OutputPath}";
    }
}
