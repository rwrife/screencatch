namespace ScreenCatch.Core.Capture;

public interface IScreenCaptureSource : IAsyncDisposable
{
    event EventHandler<CaptureProgress>? Progress;

    bool IsCapturing { get; }

    Task StartAsync(CaptureRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaptureFrame>> StopAsync(CancellationToken cancellationToken = default);

    Task CancelAsync();
}
