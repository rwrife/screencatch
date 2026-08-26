using System.Windows.Input;
using ScreenCatch.App.Services;
using ScreenCatch.Core.Ai;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Cursor;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.App.ViewModels;

public sealed class RecordingViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IRecordingSession _session;
    private readonly ICountdownService _countdown;
    private readonly Func<string> _outputPathFactory;
    private readonly IPresetStore _presetStore;
    private readonly IRecordingAiService _aiService;
    private readonly bool _ownsAiService;
    private readonly CancellationTokenSource _lifetime = new();

    private CaptureSourceKind _selectedSource = CaptureSourceKind.Screen;
    private VideoOutputFormat _selectedFormat = VideoOutputFormat.Mp4;
    private int _framesPerSecond = 30;
    private int _quality = 23;
    private int _countdownSeconds = 3;
    private int _regionX = 100;
    private int _regionY = 100;
    private int _regionWidth = 960;
    private int _regionHeight = 540;
    private string _monitorId = "primary";
    private string _windowTitle = "Terminal";
    private bool _cursorHighlightEnabled = true;
    private bool _clickEffectsEnabled = true;
    private bool _isBusy;
    private RecordingSessionState _sessionState;
    private string _statusText = "Ready to record";
    private string? _outputPath;
    private TimeSpan _elapsed;
    private bool _isAiEnabled;
    private string _aiEndpoint = "http://localhost:11434/v1/";
    private string _aiModel = "qwen2.5:3b";
    private string? _suggestedTitle;
    private string? _suggestedCaption;
    private bool _aiUsedFallback;
    private DateTimeOffset _recordedAtUtc;
    private bool _disposed;

    public RecordingViewModel(
        IRecordingSession session,
        ICountdownService countdown,
        Func<string> outputPathFactory,
        IPresetStore? presetStore = null,
        IRecordingAiService? aiService = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _countdown = countdown ?? throw new ArgumentNullException(nameof(countdown));
        _outputPathFactory = outputPathFactory ?? throw new ArgumentNullException(nameof(outputPathFactory));
        _presetStore = presetStore ?? new JsonPresetStore();
        _ownsAiService = aiService is null;
        _aiService = aiService ?? new LocalRecordingAiService();
        _sessionState = session.State;
        _session.Progress += OnSessionProgress;

        StartCommand = new AsyncCommand(StartAsync, () => CanStart);
        PauseCommand = new AsyncCommand(PauseOrResumeAsync, () => CanPause);
        StopCommand = new AsyncCommand(StopAsync, () => CanStop);
        CancelCommand = new AsyncCommand(CancelAsync, () => CanCancel);
    }

    public IReadOnlyList<CaptureSourceKind> CaptureSources { get; } = Enum.GetValues<CaptureSourceKind>();

    public IReadOnlyList<VideoOutputFormat> OutputFormats { get; } = Enum.GetValues<VideoOutputFormat>();

    public IReadOnlyList<int> FrameRateOptions { get; } = [10, 15, 24, 30, 60];

    public IReadOnlyList<int> CountdownOptions { get; } = [0, 3, 5, 10];

    public ICommand StartCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand CancelCommand { get; }

    public CaptureSourceKind SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                OnPropertyChanged(nameof(IsRegionSelected));
                OnPropertyChanged(nameof(IsMonitorSelected));
                OnPropertyChanged(nameof(IsWindowSelected));
            }
        }
    }

    public VideoOutputFormat SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    public int FramesPerSecond
    {
        get => _framesPerSecond;
        set => SetProperty(ref _framesPerSecond, Math.Clamp(value, 1, 120));
    }

    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, Math.Clamp(value, 0, 51));
    }

    public int CountdownSeconds
    {
        get => _countdownSeconds;
        set => SetProperty(ref _countdownSeconds, Math.Clamp(value, 0, 10));
    }

    public int RegionX
    {
        get => _regionX;
        set => SetProperty(ref _regionX, value);
    }

    public int RegionY
    {
        get => _regionY;
        set => SetProperty(ref _regionY, value);
    }

    public int RegionWidth
    {
        get => _regionWidth;
        set => SetProperty(ref _regionWidth, Math.Max(1, value));
    }

    public int RegionHeight
    {
        get => _regionHeight;
        set => SetProperty(ref _regionHeight, Math.Max(1, value));
    }

    public string MonitorId
    {
        get => _monitorId;
        set => SetProperty(ref _monitorId, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    public bool CursorHighlightEnabled
    {
        get => _cursorHighlightEnabled;
        set => SetProperty(ref _cursorHighlightEnabled, value);
    }

    public bool ClickEffectsEnabled
    {
        get => _clickEffectsEnabled;
        set => SetProperty(ref _clickEffectsEnabled, value);
    }

    public bool IsAiEnabled
    {
        get => _isAiEnabled;
        set => SetProperty(ref _isAiEnabled, value);
    }

    public string AiEndpoint
    {
        get => _aiEndpoint;
        set => SetProperty(ref _aiEndpoint, value);
    }

    public string AiModel
    {
        get => _aiModel;
        set => SetProperty(ref _aiModel, value);
    }

    public string? SuggestedTitle
    {
        get => _suggestedTitle;
        private set
        {
            if (SetProperty(ref _suggestedTitle, value))
            {
                OnPropertyChanged(nameof(HasAiSuggestion));
            }
        }
    }

    public string? SuggestedCaption
    {
        get => _suggestedCaption;
        private set => SetProperty(ref _suggestedCaption, value);
    }

    public bool AiUsedFallback
    {
        get => _aiUsedFallback;
        private set => SetProperty(ref _aiUsedFallback, value);
    }

    public bool HasAiSuggestion => !string.IsNullOrWhiteSpace(SuggestedTitle);

    public RecordingSessionState SessionState
    {
        get => _sessionState;
        private set
        {
            if (SetProperty(ref _sessionState, value))
            {
                NotifyControlStateChanged();
                OnPropertyChanged(nameof(PauseButtonText));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? OutputPath
    {
        get => _outputPath;
        private set
        {
            if (SetProperty(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(HasOutput));
                OnPropertyChanged(nameof(OutputFileName));
            }
        }
    }

    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set
        {
            if (SetProperty(ref _elapsed, value))
            {
                OnPropertyChanged(nameof(ElapsedText));
            }
        }
    }

    public string ElapsedText => Elapsed.ToString(@"hh\:mm\:ss");

    public string PauseButtonText => SessionState == RecordingSessionState.Paused ? "Resume" : "Pause";

    public bool IsRegionSelected => SelectedSource == CaptureSourceKind.Region;

    public bool IsMonitorSelected => SelectedSource == CaptureSourceKind.Monitor;

    public bool IsWindowSelected => SelectedSource == CaptureSourceKind.Window;

    public bool HasOutput => !string.IsNullOrWhiteSpace(OutputPath);

    public string OutputFileName => OutputPath is null ? "No recording yet" : Path.GetFileName(OutputPath);

    public bool CanStart => !_isBusy && SessionState is RecordingSessionState.Idle
        or RecordingSessionState.Completed
        or RecordingSessionState.Canceled
        or RecordingSessionState.Failed;

    public bool CanPause => !_isBusy && SessionState is RecordingSessionState.Running or RecordingSessionState.Paused;

    public bool CanStop => !_isBusy && SessionState is RecordingSessionState.Running or RecordingSessionState.Paused;

    public bool CanCancel => !_isBusy && SessionState is RecordingSessionState.Running
        or RecordingSessionState.Paused
        or RecordingSessionState.Stopping
        or RecordingSessionState.Encoding;

    public async Task StartAsync()
    {
        if (!CanStart)
        {
            return;
        }

        SetBusy(true);
        OutputPath = null;
        SuggestedTitle = null;
        SuggestedCaption = null;
        AiUsedFallback = false;
        Elapsed = TimeSpan.Zero;
        try
        {
            await _countdown.RunAsync(
                CountdownSeconds,
                remaining => StatusText = $"Recording in {remaining}…",
                _lifetime.Token);

            StatusText = "Starting capture…";
            _recordedAtUtc = DateTimeOffset.UtcNow;
            await _session.StartAsync(BuildRequest(), _lifetime.Token);
            SessionState = _session.State;
            StatusText = "Recording";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Countdown canceled";
        }
        catch (Exception exception)
        {
            SessionState = RecordingSessionState.Failed;
            StatusText = $"Unable to start: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task PauseOrResumeAsync()
    {
        if (!CanPause)
        {
            return;
        }

        if (SessionState == RecordingSessionState.Paused)
        {
            await _session.ResumeAsync();
            SessionState = _session.State;
            StatusText = "Recording";
        }
        else
        {
            await _session.PauseAsync();
            SessionState = _session.State;
            StatusText = "Paused";
        }
    }

    public async Task StopAsync()
    {
        if (!CanStop)
        {
            return;
        }

        SetBusy(true);
        StatusText = "Encoding…";
        try
        {
            var result = await _session.StopAsync(_lifetime.Token);
            SessionState = result.FinalState;
            if (result.EncodeResult.IsSuccess)
            {
                OutputPath = result.EncodeResult.OutputPath;
                if (IsAiEnabled)
                {
                    var suggestion = await _aiService.SuggestAsync(
                        new RecordingAiRequest(
                            _recordedAtUtc,
                            TimeSpan.FromSeconds(result.CapturedFrames.Count / (double)FramesPerSecond),
                            SelectedSource),
                        new RecordingAiOptions(
                            Enabled: true,
                            Endpoint: AiEndpoint,
                            Model: AiModel),
                        _lifetime.Token);
                    SuggestedTitle = suggestion.Title;
                    SuggestedCaption = suggestion.Caption;
                    AiUsedFallback = suggestion.IsFallback;
                }

                StatusText = "Ready to preview";
            }
            else
            {
                StatusText = result.EncodeResult.Error?.Message ?? "Encoding failed";
            }
        }
        catch (Exception exception)
        {
            SessionState = RecordingSessionState.Failed;
            StatusText = $"Unable to stop: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task CancelAsync()
    {
        if (!CanCancel)
        {
            return;
        }

        await _session.CancelAsync();
        SessionState = _session.State;
        StatusText = "Recording canceled";
    }

    public void SetRegion(CaptureRect region)
    {
        RegionX = region.X;
        RegionY = region.Y;
        RegionWidth = region.Width;
        RegionHeight = region.Height;
        SelectedSource = CaptureSourceKind.Region;
        StatusText = $"Region selected: {region.Width} × {region.Height}";
    }

    public void ApplyPreset(RecordingPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.Validate();

        SelectedSource = preset.Source;
        FramesPerSecond = preset.FramesPerSecond;
        SelectedFormat = preset.Format;
        Quality = preset.Quality;
        MonitorId = preset.MonitorId ?? MonitorId;
        WindowTitle = preset.WindowTitle ?? WindowTitle;
        if (preset.Region is { } region)
        {
            RegionX = region.X;
            RegionY = region.Y;
            RegionWidth = region.Width;
            RegionHeight = region.Height;
        }
    }

    public RecordingPreset CreatePreset(string name)
    {
        CaptureRect? region = SelectedSource == CaptureSourceKind.Region
            ? new CaptureRect(RegionX, RegionY, RegionWidth, RegionHeight)
            : null;
        return new RecordingPreset(
            name,
            SelectedSource,
            FramesPerSecond,
            SelectedFormat,
            Quality,
            region,
            SelectedSource == CaptureSourceKind.Monitor ? MonitorId : null,
            SelectedSource == CaptureSourceKind.Window ? WindowTitle : null);
    }

    public Task SavePresetAsync(string name, CancellationToken cancellationToken = default) =>
        _presetStore.SaveAsync(CreatePreset(name), cancellationToken);

    public async Task LoadPresetAsync(string name, CancellationToken cancellationToken = default)
    {
        var preset = await _presetStore.LoadAsync(name, cancellationToken)
            ?? throw new FileNotFoundException($"Preset '{name}' was not found.");
        ApplyPreset(preset);
    }

    public Task<IReadOnlyList<string>> ListPresetsAsync(CancellationToken cancellationToken = default) =>
        _presetStore.ListAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Progress -= OnSessionProgress;
        _lifetime.Cancel();
        await _session.DisposeAsync();
        if (_ownsAiService && _aiService is IDisposable disposableAiService)
        {
            disposableAiService.Dispose();
        }

        _lifetime.Dispose();
    }

    private RecordingSessionRequest BuildRequest()
    {
        CaptureSourceDescriptor descriptor = SelectedSource switch
        {
            CaptureSourceKind.Screen => new FullScreenCaptureDescriptor(),
            CaptureSourceKind.Monitor => new MonitorCaptureDescriptor(MonitorId),
            CaptureSourceKind.Window => new WindowCaptureDescriptor(WindowTitle: WindowTitle),
            CaptureSourceKind.Region => new RegionCaptureDescriptor(
                new CaptureRect(RegionX, RegionY, RegionWidth, RegionHeight)),
            _ => throw new InvalidOperationException($"Unsupported capture source: {SelectedSource}"),
        };
        var outputPath = Path.ChangeExtension(
            _outputPathFactory(),
            SelectedFormat == VideoOutputFormat.Mp4 ? ".mp4" : ".webm");
        var capture = new CaptureRequest(
            descriptor,
            FramesPerSecond,
            maxFrames: checked(FramesPerSecond * 60 * 60));
        var video = new VideoEncodeOptions(
            outputPath,
            SelectedFormat,
            FramesPerSecond,
            constantRateFactor: Quality);
        var cursor = new CursorOverlayOptions(CursorHighlightEnabled, ClickEffectsEnabled);
        return new RecordingSessionRequest(capture, video, cursorOverlayOptions: cursor);
    }

    private void OnSessionProgress(object? sender, RecordingSessionProgress progress)
    {
        SessionState = progress.State;
        if (progress.CaptureProgress is not null)
        {
            Elapsed = progress.CaptureProgress.Elapsed;
        }

        if (progress.EncodingProgress is not null)
        {
            StatusText = $"Encoding {progress.EncodingProgress.PercentComplete:0}%";
        }
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        NotifyControlStateChanged();
    }

    private void NotifyControlStateChanged()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanCancel));
        ((AsyncCommand)StartCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)PauseCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)StopCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)CancelCommand).RaiseCanExecuteChanged();
    }
}
