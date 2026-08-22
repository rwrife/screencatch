namespace ScreenCatch.Core.Recording;

public interface IAudioCapture : IAsyncDisposable
{
    bool IsCapturing { get; }

    Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default);

    Task<AudioCaptureResult?> StopAsync(CancellationToken cancellationToken = default);

    Task CancelAsync();
}
