namespace ScreenCatch.Core.Recording;

/// <summary>
/// Stub audio-capture implementation. It satisfies orchestration wiring while
/// native loopback/mic capture backends are implemented in later milestones.
/// </summary>
public sealed class NullAudioCapture : IAudioCapture
{
    public bool IsCapturing { get; private set; }

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        IsCapturing = true;
        return Task.CompletedTask;
    }

    public Task<AudioCaptureResult?> StopAsync(CancellationToken cancellationToken = default)
    {
        IsCapturing = false;
        return Task.FromResult<AudioCaptureResult?>(null);
    }

    public Task CancelAsync()
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsCapturing = false;
        return ValueTask.CompletedTask;
    }
}
