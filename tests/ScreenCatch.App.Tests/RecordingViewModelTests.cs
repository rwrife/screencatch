using ScreenCatch.App.Services;
using ScreenCatch.App.ViewModels;
using ScreenCatch.Core.Ai;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.App.Tests;

public sealed class RecordingViewModelTests
{
    [Fact]
    public async Task StartAsync_CountsDownAndBuildsTheSelectedRegionRequest()
    {
        var session = new FakeRecordingSession();
        var countdown = new ImmediateCountdown();
        await using var viewModel = new RecordingViewModel(
            session,
            countdown,
            () => "/tmp/region.webm");
        viewModel.SelectedSource = CaptureSourceKind.Region;
        viewModel.RegionX = -320;
        viewModel.RegionY = 40;
        viewModel.RegionWidth = 960;
        viewModel.RegionHeight = 540;
        viewModel.SelectedFormat = VideoOutputFormat.WebM;
        viewModel.FramesPerSecond = 24;
        viewModel.Quality = 31;
        viewModel.CountdownSeconds = 3;
        viewModel.CursorHighlightEnabled = true;
        viewModel.ClickEffectsEnabled = true;

        await viewModel.StartAsync();

        Assert.Equal(3, countdown.LastDuration);
        Assert.NotNull(session.LastRequest);
        var region = Assert.IsType<RegionCaptureDescriptor>(session.LastRequest!.CaptureRequest.Descriptor);
        Assert.Equal(new CaptureRect(-320, 40, 960, 540), region.Region);
        Assert.Equal(24, session.LastRequest.CaptureRequest.TargetFps);
        Assert.Equal(VideoOutputFormat.WebM, session.LastRequest.VideoOptions.Format);
        Assert.Equal(31, session.LastRequest.VideoOptions.ConstantRateFactor);
        Assert.Equal(
            new ScreenCatch.Core.Cursor.CursorOverlayOptions(
                HighlightEnabled: true,
                ClickEffectsEnabled: true),
            session.LastRequest.CursorOverlayOptions);
        Assert.True(viewModel.CursorHighlightEnabled);
        Assert.True(viewModel.ClickEffectsEnabled);
        Assert.Equal(RecordingSessionState.Running, viewModel.SessionState);
        Assert.Equal("Recording", viewModel.StatusText);
    }

    [Fact]
    public async Task PauseOrResumeAsync_TogglesTheRecordingSession()
    {
        var session = new FakeRecordingSession();
        await using var viewModel = new RecordingViewModel(
            session,
            new ImmediateCountdown(),
            () => "/tmp/demo.mp4");

        await viewModel.StartAsync();
        await viewModel.PauseOrResumeAsync();

        Assert.Equal(1, session.PauseCalls);
        Assert.Equal(RecordingSessionState.Paused, viewModel.SessionState);
        Assert.Equal("Resume", viewModel.PauseButtonText);

        await viewModel.PauseOrResumeAsync();

        Assert.Equal(1, session.ResumeCalls);
        Assert.Equal(RecordingSessionState.Running, viewModel.SessionState);
        Assert.Equal("Pause", viewModel.PauseButtonText);
    }

    [Fact]
    public async Task StopAsync_ExposesEncodedOutputForPreview()
    {
        var session = new FakeRecordingSession();
        await using var viewModel = new RecordingViewModel(
            session,
            new ImmediateCountdown(),
            () => "/tmp/demo.mp4");

        await viewModel.StartAsync();
        await viewModel.StopAsync();

        Assert.Equal("/tmp/demo.mp4", viewModel.OutputPath);
        Assert.True(viewModel.HasOutput);
        Assert.Equal("Ready to preview", viewModel.StatusText);
        Assert.Equal(RecordingSessionState.Completed, viewModel.SessionState);
    }

    [Fact]
    public async Task StopAsync_WhenAiIsOptedIn_ExposesSuggestedTitleAndCaption()
    {
        var aiService = new FakeAiService();
        await using var viewModel = new RecordingViewModel(
            new FakeRecordingSession(),
            new ImmediateCountdown(),
            () => "/tmp/demo.mp4",
            aiService: aiService);
        Assert.False(viewModel.IsAiEnabled);
        viewModel.IsAiEnabled = true;
        viewModel.AiEndpoint = "http://localhost:1234/v1/";
        viewModel.AiModel = "tiny-local";

        await viewModel.StartAsync();
        await viewModel.StopAsync();

        Assert.NotNull(aiService.Options);
        Assert.True(aiService.Options!.Enabled);
        Assert.Equal("Local title", viewModel.SuggestedTitle);
        Assert.Equal("Local caption", viewModel.SuggestedCaption);
        Assert.False(viewModel.AiUsedFallback);
    }

    [Fact]
    public async Task ApplyPreset_UsesTheSameSettingsThatTheViewModelExports()
    {
        await using var viewModel = new RecordingViewModel(
            new FakeRecordingSession(),
            new ImmediateCountdown(),
            () => "/tmp/demo.mp4");
        var preset = new RecordingPreset(
            "shared",
            CaptureSourceKind.Region,
            FramesPerSecond: 15,
            Format: VideoOutputFormat.WebM,
            Quality: 36,
            Region: new CaptureRect(5, 6, 640, 360),
            Audio: PresetAudioMode.None);

        viewModel.ApplyPreset(preset);
        var exported = viewModel.CreatePreset("gui-copy");

        Assert.Equal("gui-copy", exported.Name);
        Assert.Equal(preset.Source, exported.Source);
        Assert.Equal(preset.FramesPerSecond, exported.FramesPerSecond);
        Assert.Equal(preset.Format, exported.Format);
        Assert.Equal(preset.Quality, exported.Quality);
        Assert.Equal(preset.Region, exported.Region);
    }

    [Fact]
    public async Task SaveAndLoadPresetAsync_RoundTripsThroughTheSharedStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"screencatch-vm-presets-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonPresetStore(directory);
            await using var viewModel = new RecordingViewModel(
                new FakeRecordingSession(),
                new ImmediateCountdown(),
                () => "/tmp/demo.mp4",
                store);
            viewModel.SelectedSource = CaptureSourceKind.Region;
            viewModel.SetRegion(new CaptureRect(7, 8, 800, 450));
            viewModel.FramesPerSecond = 12;

            await viewModel.SavePresetAsync("shared");
            viewModel.FramesPerSecond = 60;
            await viewModel.LoadPresetAsync("shared");

            Assert.Equal(12, viewModel.FramesPerSecond);
            Assert.Equal(new CaptureRect(7, 8, 800, 450), viewModel.CreatePreset("loaded").Region);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class FakeAiService : IRecordingAiService
    {
        public RecordingAiOptions? Options { get; private set; }

        public Task<bool> ProbeAsync(RecordingAiOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<RecordingAiSuggestion> SuggestAsync(
            RecordingAiRequest request,
            RecordingAiOptions options,
            CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new RecordingAiSuggestion("Local title", "Local caption", IsFallback: false));
        }
    }

    private sealed class ImmediateCountdown : ICountdownService
    {
        public int LastDuration { get; private set; }

        public Task RunAsync(int seconds, Action<int> tick, CancellationToken cancellationToken = default)
        {
            LastDuration = seconds;
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                tick(remaining);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecordingSession : IRecordingSession
    {
        public event EventHandler<RecordingSessionProgress>? Progress;

        public RecordingSessionState State { get; private set; } = RecordingSessionState.Idle;

        public RecordingSessionRequest? LastRequest { get; private set; }

        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public Task StartAsync(RecordingSessionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            State = RecordingSessionState.Running;
            Progress?.Invoke(this, new RecordingSessionProgress(State));
            return Task.CompletedTask;
        }

        public Task<RecordingSessionResult> StopAsync(CancellationToken cancellationToken = default)
        {
            State = RecordingSessionState.Completed;
            Progress?.Invoke(this, new RecordingSessionProgress(State));
            var output = LastRequest?.VideoOptions.OutputPath ?? "/tmp/demo.mp4";
            return Task.FromResult(new RecordingSessionResult(
                State,
                Array.Empty<CaptureFrame>(),
                VideoEncodeResult.Success(output)));
        }

        public Task PauseAsync()
        {
            PauseCalls++;
            State = RecordingSessionState.Paused;
            Progress?.Invoke(this, new RecordingSessionProgress(State));
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            ResumeCalls++;
            State = RecordingSessionState.Running;
            Progress?.Invoke(this, new RecordingSessionProgress(State));
            return Task.CompletedTask;
        }

        public Task CancelAsync()
        {
            State = RecordingSessionState.Canceled;
            Progress?.Invoke(this, new RecordingSessionProgress(State));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
