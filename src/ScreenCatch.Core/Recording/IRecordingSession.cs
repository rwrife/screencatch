namespace ScreenCatch.Core.Recording;

public interface IRecordingSession : IAsyncDisposable
{
    event EventHandler<RecordingSessionProgress>? Progress;

    RecordingSessionState State { get; }

    Task StartAsync(RecordingSessionRequest request, CancellationToken cancellationToken = default);

    Task<RecordingSessionResult> StopAsync(CancellationToken cancellationToken = default);

    Task PauseAsync();

    Task ResumeAsync();

    Task CancelAsync();
}
