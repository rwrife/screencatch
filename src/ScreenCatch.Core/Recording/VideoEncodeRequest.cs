using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Recording;

public sealed class VideoEncodeRequest
{
    public VideoEncodeRequest(
        IReadOnlyList<CaptureFrame> frames,
        VideoEncodeOptions options,
        AudioCaptureResult? audioTrack = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(options);

        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required for encoding.", nameof(frames));
        }

        var first = frames[0] ?? throw new ArgumentException("Frame collection contains null entries.", nameof(frames));
        var width = first.Width;
        var height = first.Height;

        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i] ?? throw new ArgumentException("Frame collection contains null entries.", nameof(frames));
            if (frame.Width != width || frame.Height != height)
            {
                throw new ArgumentException("All frames must have the same dimensions.", nameof(frames));
            }

            if (frame.Stride < width * 4)
            {
                throw new ArgumentException("Frame stride must be >= width * 4.", nameof(frames));
            }
        }

        Frames = frames.ToArray();
        Options = options;
        AudioTrack = audioTrack;
        Width = width;
        Height = height;
    }

    public IReadOnlyList<CaptureFrame> Frames { get; }

    public VideoEncodeOptions Options { get; }

    public AudioCaptureResult? AudioTrack { get; }

    public int Width { get; }

    public int Height { get; }
}
