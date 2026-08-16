using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Recording;

public sealed class RecordingSession : IRecordingSession
{
    private readonly IScreenCaptureSource _captureSource;
    private readonly IVideoEncoder _videoEncoder;
    private readonly IAudioCapture _audioCapture;
    private readonly object _sync = new();

    private RecordingSessionRequest? _request;
    private bool _disposed;

    public RecordingSession(
        IScreenCaptureSource captureSource,
        IVideoEncoder videoEncoder,
        IAudioCapture? audioCapture = null)
    {
        _captureSource = captureSource ?? throw new ArgumentNullException(nameof(captureSource));
        _videoEncoder = videoEncoder ?? throw new ArgumentNullException(nameof(videoEncoder));
        _audioCapture = audioCapture ?? new NullAudioCapture();

        _captureSource.Progress += OnCaptureProgress;
        _videoEncoder.Progress += OnEncoderProgress;
    }

    public event EventHandler<RecordingSessionProgress>? Progress;

    public RecordingSessionState State { get; private set; } = RecordingSessionState.Idle;

    public async Task StartAsync(RecordingSessionRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        lock (_sync)
        {
            if (State is not RecordingSessionState.Idle and not RecordingSessionState.Completed and not RecordingSessionState.Canceled and not RecordingSessionState.Failed)
            {
                throw new InvalidOperationException("Recording session is already active.");
            }

            _request = request;
            State = RecordingSessionState.Running;
        }

        if (request.AudioOptions is not null)
        {
            await _audioCapture.StartAsync(request.AudioOptions, cancellationToken).ConfigureAwait(false);
        }

        await _captureSource.StartAsync(request.CaptureRequest, cancellationToken).ConfigureAwait(false);
        Progress?.Invoke(this, new RecordingSessionProgress(State));
    }

    public async Task<RecordingSessionResult> StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        RecordingSessionRequest request;
        lock (_sync)
        {
            if (_request is null)
            {
                throw new InvalidOperationException("Recording session has not been started.");
            }

            if (State is not RecordingSessionState.Running and not RecordingSessionState.Paused)
            {
                throw new InvalidOperationException($"Cannot stop session while state is {State}.");
            }

            State = RecordingSessionState.Stopping;
            request = _request;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State));

        var frames = await _captureSource.StopAsync(cancellationToken).ConfigureAwait(false);
        AudioCaptureResult? audioTrack = null;

        if (request.AudioOptions is not null)
        {
            audioTrack = await _audioCapture.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_sync)
        {
            State = RecordingSessionState.Encoding;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State));

        var encodeRequest = new VideoEncodeRequest(frames, request.VideoOptions, audioTrack);
        var encodeResult = await _videoEncoder.EncodeAsync(encodeRequest, cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            State = encodeResult.IsSuccess
                ? RecordingSessionState.Completed
                : encodeResult.Error?.Code == VideoEncodeErrorCode.Canceled
                    ? RecordingSessionState.Canceled
                    : RecordingSessionState.Failed;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State));
        return new RecordingSessionResult(State, frames, encodeResult);
    }

    public Task PauseAsync()
    {
        ThrowIfDisposed();

        lock (_sync)
        {
            if (State != RecordingSessionState.Running)
            {
                throw new InvalidOperationException($"Cannot pause session while state is {State}.");
            }

            State = RecordingSessionState.Paused;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State));
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        ThrowIfDisposed();

        lock (_sync)
        {
            if (State != RecordingSessionState.Paused)
            {
                throw new InvalidOperationException($"Cannot resume session while state is {State}.");
            }

            State = RecordingSessionState.Running;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State));
        return Task.CompletedTask;
    }

    public async Task CancelAsync()
    {
        if (_disposed)
        {
            return;
        }

        RecordingSessionState previousState;
        lock (_sync)
        {
            previousState = State;
            if (State is RecordingSessionState.Canceled or RecordingSessionState.Completed or RecordingSessionState.Idle)
            {
                return;
            }

            State = RecordingSessionState.Canceled;
        }

        if (previousState is RecordingSessionState.Running or RecordingSessionState.Paused or RecordingSessionState.Stopping)
        {
            await _captureSource.CancelAsync().ConfigureAwait(false);
        }

        await _audioCapture.CancelAsync().ConfigureAwait(false);
        Progress?.Invoke(this, new RecordingSessionProgress(State));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _captureSource.Progress -= OnCaptureProgress;
        _videoEncoder.Progress -= OnEncoderProgress;

        await CancelAsync().ConfigureAwait(false);
        await _captureSource.DisposeAsync().ConfigureAwait(false);
        await _audioCapture.DisposeAsync().ConfigureAwait(false);

        _disposed = true;
    }

    private void OnCaptureProgress(object? sender, CaptureProgress progress)
    {
        var state = State;
        if (state is not RecordingSessionState.Running and not RecordingSessionState.Paused)
        {
            return;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(state, CaptureProgress: progress));
    }

    private void OnEncoderProgress(object? sender, VideoEncoderProgress progress)
    {
        if (State != RecordingSessionState.Encoding)
        {
            return;
        }

        Progress?.Invoke(this, new RecordingSessionProgress(State, EncodingProgress: progress));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RecordingSession));
        }
    }
}
