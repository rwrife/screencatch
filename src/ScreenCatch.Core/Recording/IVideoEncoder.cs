namespace ScreenCatch.Core.Recording;

public interface IVideoEncoder
{
    event EventHandler<VideoEncoderProgress>? Progress;

    Task<VideoEncodeResult> EncodeAsync(VideoEncodeRequest request, CancellationToken cancellationToken = default);
}
