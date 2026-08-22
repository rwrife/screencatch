using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class RecordingSessionTests
{
    [Fact]
    public async Task StartStop_TransitionsToCompleted_AndEncodesCapturedFrames()
    {
        var fakeCaptureSource = new FakeCaptureSource(CreateFrames(count: 5, width: 16, height: 12));
        var fakeEncoder = new FakeVideoEncoder(VideoEncodeResult.Success("/tmp/demo.mp4"));
        var fakeAudio = new FakeAudioCapture(audioResult: null);

        await using var session = new RecordingSession(fakeCaptureSource, fakeEncoder, fakeAudio);

        var states = new List<RecordingSessionState>();
        session.Progress += (_, progress) => states.Add(progress.State);

        var request = new RecordingSessionRequest(
            captureRequest: new CaptureRequest(new FullScreenCaptureDescriptor(), targetFps: 30, maxFrames: 5),
            videoOptions: new VideoEncodeOptions("/tmp/demo.mp4", VideoOutputFormat.Mp4, framesPerSecond: 30),
            audioOptions: new AudioCaptureOptions(IncludeSystemAudio: true, IncludeMicrophone: false));

        await session.StartAsync(request);
        var result = await session.StopAsync();

        Assert.Equal(RecordingSessionState.Completed, result.FinalState);
        Assert.Equal(RecordingSessionState.Completed, session.State);
        Assert.True(result.EncodeResult.IsSuccess);
        Assert.NotNull(fakeEncoder.LastRequest);
        Assert.Equal(5, fakeEncoder.LastRequest!.Frames.Count);
        Assert.Contains(RecordingSessionState.Running, states);
        Assert.Contains(RecordingSessionState.Encoding, states);
    }

    [Fact]
    public async Task PauseResume_TransitionsStates()
    {
        var fakeCaptureSource = new FakeCaptureSource(CreateFrames(count: 2, width: 8, height: 8));
        var fakeEncoder = new FakeVideoEncoder(VideoEncodeResult.Success("/tmp/demo.mp4"));

        await using var session = new RecordingSession(fakeCaptureSource, fakeEncoder);

        var request = new RecordingSessionRequest(
            captureRequest: new CaptureRequest(new FullScreenCaptureDescriptor(), targetFps: 10, maxFrames: 2),
            videoOptions: new VideoEncodeOptions("/tmp/demo.mp4", VideoOutputFormat.Mp4, framesPerSecond: 10));

        await session.StartAsync(request);
        Assert.Equal(RecordingSessionState.Running, session.State);

        await session.PauseAsync();
        Assert.Equal(RecordingSessionState.Paused, session.State);

        await session.ResumeAsync();
        Assert.Equal(RecordingSessionState.Running, session.State);

        _ = await session.StopAsync();
    }

    [Fact]
    public async Task CancelAsync_CancelsCapture_AndMarksSessionCanceled()
    {
        var fakeCaptureSource = new FakeCaptureSource(CreateFrames(count: 10, width: 8, height: 6));
        var fakeEncoder = new FakeVideoEncoder(VideoEncodeResult.Success("/tmp/demo.mp4"));

        await using var session = new RecordingSession(fakeCaptureSource, fakeEncoder);

        var request = new RecordingSessionRequest(
            captureRequest: new CaptureRequest(new FullScreenCaptureDescriptor(), targetFps: 30, maxFrames: 10),
            videoOptions: new VideoEncodeOptions("/tmp/demo.mp4", VideoOutputFormat.Mp4, framesPerSecond: 30));

        await session.StartAsync(request);
        await session.CancelAsync();

        Assert.Equal(RecordingSessionState.Canceled, session.State);
        Assert.True(fakeCaptureSource.CancelCalled);
        Assert.Equal(0, fakeEncoder.EncodeCalls);
    }

    [Fact]
    public async Task CancelAsync_DuringEncoding_CancelsEncoder_AndKeepsSessionCanceled()
    {
        var fakeCaptureSource = new FakeCaptureSource(CreateFrames(count: 3, width: 8, height: 6));
        var blockingEncoder = new BlockingVideoEncoder();

        await using var session = new RecordingSession(fakeCaptureSource, blockingEncoder);

        var request = new RecordingSessionRequest(
            captureRequest: new CaptureRequest(new FullScreenCaptureDescriptor(), targetFps: 30, maxFrames: 3),
            videoOptions: new VideoEncodeOptions("/tmp/demo.mp4", VideoOutputFormat.Mp4, framesPerSecond: 30));

        await session.StartAsync(request);
        var stopTask = session.StopAsync();
        await blockingEncoder.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await session.CancelAsync();
            Assert.True(blockingEncoder.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            blockingEncoder.Release.TrySetResult();
        }

        var result = await stopTask;
        Assert.Equal(RecordingSessionState.Canceled, result.FinalState);
        Assert.Equal(RecordingSessionState.Canceled, session.State);
    }

    private static IReadOnlyList<CaptureFrame> CreateFrames(int count, int width, int height)
    {
        var frames = new List<CaptureFrame>(capacity: count);
        var stride = width * 4;

        for (var i = 0; i < count; i++)
        {
            var buffer = new byte[stride * height];
            frames.Add(new CaptureFrame(DateTimeOffset.UtcNow, new CaptureRect(0, 0, width, height), stride, buffer));
        }

        return frames;
    }

    private sealed class FakeCaptureSource : IScreenCaptureSource
    {
        private readonly IReadOnlyList<CaptureFrame> _frames;

        public FakeCaptureSource(IReadOnlyList<CaptureFrame> frames)
        {
            _frames = frames;
        }

        public event EventHandler<CaptureProgress>? Progress;

        public bool IsCapturing { get; private set; }

        public bool CancelCalled { get; private set; }

        public Task StartAsync(CaptureRequest request, CancellationToken cancellationToken = default)
        {
            IsCapturing = true;
            Progress?.Invoke(this, new CaptureProgress(1, request.MaxFrames, TimeSpan.Zero));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CaptureFrame>> StopAsync(CancellationToken cancellationToken = default)
        {
            IsCapturing = false;
            return Task.FromResult(_frames);
        }

        public Task CancelAsync()
        {
            CancelCalled = true;
            IsCapturing = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeVideoEncoder : IVideoEncoder
    {
        private readonly VideoEncodeResult _result;

        public FakeVideoEncoder(VideoEncodeResult result)
        {
            _result = result;
        }

        public event EventHandler<VideoEncoderProgress>? Progress;

        public int EncodeCalls { get; private set; }

        public VideoEncodeRequest? LastRequest { get; private set; }

        public Task<VideoEncodeResult> EncodeAsync(VideoEncodeRequest request, CancellationToken cancellationToken = default)
        {
            EncodeCalls++;
            LastRequest = request;
            Progress?.Invoke(this, new VideoEncoderProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 100));
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingVideoEncoder : IVideoEncoder
    {
        public event EventHandler<VideoEncoderProgress>? Progress;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CancellationToken { get; private set; }

        public async Task<VideoEncodeResult> EncodeAsync(
            VideoEncodeRequest request,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            Started.TrySetResult();
            await Release.Task;

            if (cancellationToken.IsCancellationRequested)
            {
                return VideoEncodeResult.Failure(
                    new VideoEncodeError(VideoEncodeErrorCode.Canceled, "Encoding was canceled."));
            }

            Progress?.Invoke(this, new VideoEncoderProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 100));
            return VideoEncodeResult.Success(request.Options.OutputPath);
        }
    }

    private sealed class FakeAudioCapture : IAudioCapture
    {
        private readonly AudioCaptureResult? _audioResult;

        public FakeAudioCapture(AudioCaptureResult? audioResult)
        {
            _audioResult = audioResult;
        }

        public bool IsCapturing { get; private set; }

        public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
        {
            IsCapturing = true;
            return Task.CompletedTask;
        }

        public Task<AudioCaptureResult?> StopAsync(CancellationToken cancellationToken = default)
        {
            IsCapturing = false;
            return Task.FromResult(_audioResult);
        }

        public Task CancelAsync()
        {
            IsCapturing = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
