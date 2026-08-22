using System.Globalization;

namespace ScreenCatch.Core.Recording;

public sealed class FfmpegVideoEncoder : IVideoEncoder
{
    private readonly IFfmpegEngine _ffmpegEngine;

    public FfmpegVideoEncoder(IFfmpegEngine ffmpegEngine)
    {
        _ffmpegEngine = ffmpegEngine ?? throw new ArgumentNullException(nameof(ffmpegEngine));
    }

    public event EventHandler<VideoEncoderProgress>? Progress;

    public async Task<VideoEncodeResult> EncodeAsync(VideoEncodeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var outputDirectory = Path.GetDirectoryName(request.Options.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var expectedDuration = TimeSpan.FromSeconds(request.Frames.Count / (double)request.Options.FramesPerSecond);
            var arguments = FfmpegArgumentBuilder.Build(request);

            var ffmpegResult = await _ffmpegEngine.RunAsync(
                arguments,
                (stream, ct) => WriteFramesAsync(request, stream, ct),
                line => TryEmitProgress(line, expectedDuration),
                cancellationToken).ConfigureAwait(false);

            if (ffmpegResult.ExitCode != 0)
            {
                return VideoEncodeResult.Failure(
                    new VideoEncodeError(
                        VideoEncodeErrorCode.EncoderFailed,
                        $"ffmpeg exited with code {ffmpegResult.ExitCode.ToString(CultureInfo.InvariantCulture)}."),
                    ffmpegResult.ExitCode,
                    ffmpegResult.StandardErrorLines);
            }

            var fileInfo = new FileInfo(request.Options.OutputPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return VideoEncodeResult.Failure(
                    new VideoEncodeError(
                        VideoEncodeErrorCode.OutputNotCreated,
                        "ffmpeg completed but no playable output was produced."),
                    ffmpegResult.ExitCode,
                    ffmpegResult.StandardErrorLines);
            }

            Progress?.Invoke(this, new VideoEncoderProgress(expectedDuration, expectedDuration, 100));
            return VideoEncodeResult.Success(request.Options.OutputPath, ffmpegResult.StandardErrorLines);
        }
        catch (OperationCanceledException)
        {
            return VideoEncodeResult.Failure(
                new VideoEncodeError(VideoEncodeErrorCode.Canceled, "Encoding was canceled."));
        }
        catch (IOException ex)
        {
            return VideoEncodeResult.Failure(
                new VideoEncodeError(VideoEncodeErrorCode.IoFailure, ex.Message));
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            return VideoEncodeResult.Failure(
                new VideoEncodeError(VideoEncodeErrorCode.EncoderFailed, ex.Message));
        }
    }

    private void TryEmitProgress(string line, TimeSpan expectedDuration)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        TimeSpan encodedDuration;

        if (line.StartsWith("out_time=", StringComparison.Ordinal))
        {
            var value = line["out_time=".Length..];
            if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out encodedDuration))
            {
                return;
            }
        }
        else if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            var value = line["out_time_us=".Length..];
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
            {
                return;
            }

            encodedDuration = TimeSpan.FromMilliseconds(microseconds / 1000d);
        }
        else if (line.StartsWith("progress=end", StringComparison.Ordinal))
        {
            Progress?.Invoke(this, new VideoEncoderProgress(expectedDuration, expectedDuration, 100));
            return;
        }
        else
        {
            return;
        }

        var denominator = Math.Max(expectedDuration.TotalMilliseconds, 1);
        var percent = Math.Clamp((encodedDuration.TotalMilliseconds / denominator) * 100d, 0d, 100d);

        Progress?.Invoke(this, new VideoEncoderProgress(encodedDuration, expectedDuration, percent));
    }

    private static async Task WriteFramesAsync(VideoEncodeRequest request, Stream standardInput, CancellationToken cancellationToken)
    {
        var packedStride = request.Width * 4;

        foreach (var frame in request.Frames)
        {
            if (frame.Stride == packedStride)
            {
                await standardInput.WriteAsync(frame.Buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            for (var row = 0; row < frame.Height; row++)
            {
                var sourceOffset = row * frame.Stride;
                await standardInput.WriteAsync(
                    frame.Buffer.AsMemory(sourceOffset, packedStride),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
