using System.Globalization;

namespace ScreenCatch.Core.Recording;

public static class FfmpegArgumentBuilder
{
    public static IReadOnlyList<string> Build(VideoEncodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = request.Options;
        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-f", "rawvideo",
            "-pix_fmt", "bgra",
            "-video_size", $"{request.Width}x{request.Height}",
            "-framerate", options.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-i", "pipe:0",
        };

        if (request.AudioTrack is not null)
        {
            args.Add("-i");
            args.Add(request.AudioTrack.FilePath);
        }

        args.AddRange(new[] { "-progress", "pipe:2", "-nostats" });

        args.Add("-map");
        args.Add("0:v:0");

        if (request.AudioTrack is null)
        {
            args.Add("-an");
        }
        else
        {
            args.Add("-map");
            args.Add("1:a:0");
        }

        switch (options.Format)
        {
            case VideoOutputFormat.Mp4:
                args.AddRange(new[]
                {
                    "-c:v", "libx264",
                    "-preset", "veryfast",
                    "-pix_fmt", "yuv420p",
                    "-movflags", "+faststart",
                });

                if (options.TargetBitrate is not null)
                {
                    args.Add("-b:v");
                    args.Add(options.TargetBitrate);
                }
                else
                {
                    args.Add("-crf");
                    args.Add((options.ConstantRateFactor ?? 23).ToString(CultureInfo.InvariantCulture));
                }

                if (request.AudioTrack is not null)
                {
                    args.AddRange(new[] { "-c:a", "aac", "-b:a", "128k", "-shortest" });
                }

                break;

            case VideoOutputFormat.WebM:
                args.AddRange(new[]
                {
                    "-c:v", "libvpx-vp9",
                    "-row-mt", "1",
                    "-deadline", "good",
                    "-pix_fmt", "yuv420p",
                });

                if (options.TargetBitrate is not null)
                {
                    args.Add("-b:v");
                    args.Add(options.TargetBitrate);
                }
                else
                {
                    args.Add("-crf");
                    args.Add((options.ConstantRateFactor ?? 32).ToString(CultureInfo.InvariantCulture));
                    args.Add("-b:v");
                    args.Add("0");
                }

                if (request.AudioTrack is not null)
                {
                    args.AddRange(new[] { "-c:a", "libopus", "-b:a", "96k", "-shortest" });
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(options.Format), options.Format, "Unsupported output format.");
        }

        args.Add("-r");
        args.Add(options.FramesPerSecond.ToString(CultureInfo.InvariantCulture));
        args.Add("-frames:v");
        args.Add(request.Frames.Count.ToString(CultureInfo.InvariantCulture));
        args.Add(options.OutputPath);

        return args;
    }
}
