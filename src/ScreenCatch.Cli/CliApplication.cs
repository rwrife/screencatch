using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenCatch.Core.Ai;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Editing;
using ScreenCatch.Core.Export;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Cli;

public enum CliExitCode
{
    Success = 0,
    Usage = 2,
    InputNotFound = 3,
    ToolUnavailable = 4,
    OperationFailed = 5,
    Canceled = 130,
}

public sealed class CliApplication : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TextWriter _output;
    private readonly IPresetStore _presetStore;
    private readonly IFfmpegEngine _ffmpegEngine;
    private readonly IMediaProbe _mediaProbe;
    private readonly Func<IScreenCaptureSource> _captureSourceFactory;
    private readonly IRecordingAiService _aiService;
    private readonly bool _ownsAiService;
    private bool _disposed;

    public CliApplication(
        TextWriter output,
        IPresetStore? presetStore = null,
        IFfmpegEngine? ffmpegEngine = null,
        IMediaProbe? mediaProbe = null,
        Func<IScreenCaptureSource>? captureSourceFactory = null,
        IRecordingAiService? aiService = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _presetStore = presetStore ?? new JsonPresetStore();
        _ffmpegEngine = ffmpegEngine ?? new FfmpegProcessEngine();
        _mediaProbe = mediaProbe ?? new FfprobeMediaProbe();
        _captureSourceFactory = captureSourceFactory ?? (() => ScreenCaptureSourceFactory.CreateDefault());
        _ownsAiService = aiService is null;
        _aiService = aiService ?? new LocalRecordingAiService();
    }

    public Task<CliExitCode> RunAsync(CliInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return invocation.Verb switch
        {
            "record" => RecordAsync(invocation, cancellationToken),
            "gif" => GifAsync(invocation, cancellationToken),
            "trim" => TrimAsync(invocation, cancellationToken),
            "probe" => ProbeAsync(invocation, cancellationToken),
            "preset" => PresetAsync(invocation, cancellationToken),
            "help" => HelpAsync(invocation),
            _ => throw new CliUsageException($"Unknown command '{invocation.Verb}'."),
        };
    }

    private async Task<CliExitCode> RecordAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var outputPath = invocation.Require("out");
        RecordingPreset? saved = null;
        if (invocation.Get("preset") is { } presetName)
        {
            saved = await _presetStore.LoadAsync(presetName, cancellationToken).ConfigureAwait(false)
                ?? throw new CliUsageException($"Preset '{presetName}' was not found.");
        }

        var settings = CliValueParser.BuildPreset(invocation, saved);
        var descriptor = CreateDescriptor(settings);
        var duration = invocation.Get("duration") is { } durationValue
            ? CliValueParser.ParseTime(durationValue, "duration")
            : (TimeSpan?)null;
        if (duration == TimeSpan.Zero)
        {
            throw new CliUsageException("--duration must be greater than zero.");
        }

        var maxFrames = CliRecordingLimits.GetMaxFrames(duration, settings.FramesPerSecond);
        var captureRequest = new CaptureRequest(descriptor, settings.FramesPerSecond, maxFrames);
        var videoOptions = new VideoEncodeOptions(
            outputPath,
            settings.Format,
            settings.FramesPerSecond,
            constantRateFactor: settings.Quality);
        var audio = settings.Audio == PresetAudioMode.None
            ? null
            : new AudioCaptureOptions(
                settings.Audio is PresetAudioMode.System or PresetAudioMode.Both,
                settings.Audio is PresetAudioMode.Microphone or PresetAudioMode.Both);

        await using var session = new RecordingSession(
            _captureSourceFactory(),
            new FfmpegVideoEncoder(_ffmpegEngine));
        var recordedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            await session.StartAsync(
                new RecordingSessionRequest(captureRequest, videoOptions, audio),
                cancellationToken).ConfigureAwait(false);
            await WaitForStopAsync(duration, cancellationToken).ConfigureAwait(false);
            var result = await session.StopAsync(cancellationToken).ConfigureAwait(false);
            if (!result.EncodeResult.IsSuccess)
            {
                var exitCode = result.EncodeResult.Error?.Code == VideoEncodeErrorCode.ToolUnavailable
                    ? CliExitCode.ToolUnavailable
                    : CliExitCode.OperationFailed;
                return await WriteFailureAsync(
                    invocation,
                    result.EncodeResult.Error?.Message ?? "Recording failed.",
                    exitCode).ConfigureAwait(false);
            }

            RecordingAiSuggestion? suggestion = null;
            if (invocation.Has("ai"))
            {
                var aiOptions = new RecordingAiOptions(
                    Enabled: true,
                    Endpoint: invocation.Get("ai-endpoint") ?? "http://localhost:11434/v1/",
                    Model: invocation.Get("ai-model") ?? "qwen2.5:3b");
                suggestion = await _aiService.SuggestAsync(
                    new RecordingAiRequest(
                        recordedAtUtc,
                        TimeSpan.FromSeconds(result.CapturedFrames.Count / (double)settings.FramesPerSecond),
                        settings.Source,
                        saved?.Name),
                    aiOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            return await WriteSuccessAsync(
                invocation,
                "record",
                result.EncodeResult.OutputPath,
                suggestion: suggestion).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await session.CancelAsync().ConfigureAwait(false);
            return await WriteFailureAsync(invocation, "Recording canceled.", CliExitCode.Canceled).ConfigureAwait(false);
        }
    }

    private async Task<CliExitCode> GifAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var inputPath = RequireInput(invocation);
        var outputPath = invocation.Require("out");
        var info = await _mediaProbe.ProbeAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var start = invocation.Get("start") is { } startValue
            ? CliValueParser.ParseTime(startValue, "start")
            : TimeSpan.Zero;
        var end = invocation.Get("end") is { } endValue
            ? CliValueParser.ParseTime(endValue, "end")
            : info.Duration;
        TrimRange? range = start == TimeSpan.Zero && end == info.Duration
            ? null
            : new TrimRange(start, end);
        var format = ParseGifFormat(invocation.Get("format"), outputPath);
        var fps = invocation.Get("fps") is { } fpsValue
            ? CliValueParser.ParseInt(fpsValue, "fps", 1, 120)
            : 15;
        var width = invocation.Get("width") is { } widthValue
            ? CliValueParser.ParseInt(widthValue, "width", 1, 16384)
            : Math.Min(960, info.Width);
        var request = new GifExportRequest(
            inputPath,
            outputPath,
            info.Duration,
            info.Width,
            info.Height,
            format,
            fps,
            width,
            range: range);
        var result = await new FfmpegGifExporter(_ffmpegEngine)
            .ExportAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? await WriteSuccessAsync(invocation, "gif", result.OutputPath).ConfigureAwait(false)
            : await WriteFailureAsync(invocation, result.ErrorMessage ?? "GIF export failed.", CliExitCode.OperationFailed)
                .ConfigureAwait(false);
    }

    private async Task<CliExitCode> TrimAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var inputPath = RequireInput(invocation);
        var outputPath = invocation.Require("out");
        var start = CliValueParser.ParseTime(invocation.Require("start"), "start");
        var end = CliValueParser.ParseTime(invocation.Require("end"), "end");
        var result = await new FfmpegTrimmer(_ffmpegEngine).TrimAsync(
            new TrimRequest(inputPath, outputPath, new TrimRange(start, end), invocation.Has("frame-accurate")),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? await WriteSuccessAsync(invocation, "trim", result.OutputPath).ConfigureAwait(false)
            : await WriteFailureAsync(invocation, result.ErrorMessage ?? "Trim failed.", CliExitCode.OperationFailed)
                .ConfigureAwait(false);
    }

    private async Task<CliExitCode> ProbeAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var inputPath = RequireInput(invocation);
        var info = await _mediaProbe.ProbeAsync(inputPath, cancellationToken).ConfigureAwait(false);
        if (invocation.Json)
        {
            await WriteJsonAsync(new
            {
                success = true,
                command = "probe",
                inputPath,
                durationSeconds = info.Duration.TotalSeconds,
                width = info.Width,
                height = info.Height,
            }).ConfigureAwait(false);
        }
        else
        {
            await _output.WriteLineAsync($"{inputPath}: {info.Width}x{info.Height}, {info.Duration.TotalSeconds:0.###}s")
                .ConfigureAwait(false);
        }

        return CliExitCode.Success;
    }

    private async Task<CliExitCode> PresetAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        return invocation.Action switch
        {
            "save" => await SavePresetAsync(invocation, cancellationToken).ConfigureAwait(false),
            "list" => await ListPresetsAsync(invocation, cancellationToken).ConfigureAwait(false),
            "show" => await ShowPresetAsync(invocation, cancellationToken).ConfigureAwait(false),
            "delete" => await DeletePresetAsync(invocation, cancellationToken).ConfigureAwait(false),
            _ => throw new CliUsageException("preset requires one of: save, list, show, delete."),
        };
    }

    private async Task<CliExitCode> SavePresetAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var name = RequireSinglePositional(invocation, "preset save requires a name.");
        var preset = CliValueParser.BuildPreset(invocation) with { Name = name };
        preset.Validate();
        await _presetStore.SaveAsync(preset, cancellationToken).ConfigureAwait(false);
        return await WriteSuccessAsync(invocation, "preset save", preset: preset).ConfigureAwait(false);
    }

    private async Task<CliExitCode> ListPresetsAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        EnsureNoPositional(invocation);
        var names = await _presetStore.ListAsync(cancellationToken).ConfigureAwait(false);
        if (invocation.Json)
        {
            await WriteJsonAsync(new { success = true, command = "preset list", presets = names }).ConfigureAwait(false);
        }
        else
        {
            foreach (var name in names)
            {
                await _output.WriteLineAsync(name).ConfigureAwait(false);
            }
        }

        return CliExitCode.Success;
    }

    private async Task<CliExitCode> ShowPresetAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var name = RequireSinglePositional(invocation, "preset show requires a name.");
        var preset = await _presetStore.LoadAsync(name, cancellationToken).ConfigureAwait(false);
        if (preset is null)
        {
            return await WriteFailureAsync(invocation, $"Preset '{name}' was not found.", CliExitCode.InputNotFound)
                .ConfigureAwait(false);
        }

        if (invocation.Json)
        {
            await WriteJsonAsync(new { success = true, command = "preset show", preset }).ConfigureAwait(false);
        }
        else
        {
            await _output.WriteLineAsync(JsonSerializer.Serialize(preset, JsonOptions)).ConfigureAwait(false);
        }

        return CliExitCode.Success;
    }

    private async Task<CliExitCode> DeletePresetAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        var name = RequireSinglePositional(invocation, "preset delete requires a name.");
        var deleted = await _presetStore.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
        return deleted
            ? await WriteSuccessAsync(invocation, "preset delete").ConfigureAwait(false)
            : await WriteFailureAsync(invocation, $"Preset '{name}' was not found.", CliExitCode.InputNotFound)
                .ConfigureAwait(false);
    }

    private async Task<CliExitCode> HelpAsync(CliInvocation invocation)
    {
        const string help = """
            ScreenCatch CLI

            Commands:
              record --out FILE [--source screen|monitor|window|region] [--duration SECONDS]
              gif --in FILE --out FILE [--start TIME] [--end TIME] [--fps N] [--width PX]
              trim --in FILE --out FILE --start TIME --end TIME [--frame-accurate]
              probe --in FILE
              preset save|show|delete NAME | preset list

            Shared record/preset options:
              --source, --rect X,Y,W,H, --title, --monitor, --fps, --format,
              --quality, --audio none|mic|system|both, --preset NAME, --json

            Optional local AI for record (off by default):
              --ai [--ai-endpoint http://localhost:11434/v1/] [--ai-model MODEL]
              Only loopback endpoints are accepted; failures use a timestamp fallback.

            Exit codes: 0 success, 2 usage, 3 missing input, 4 tool unavailable,
                        5 operation failed, 130 canceled.
            """;
        await _output.WriteLineAsync(help).ConfigureAwait(false);
        return CliExitCode.Success;
    }

    private static CaptureSourceDescriptor CreateDescriptor(RecordingPreset settings) => settings.Source switch
    {
        CaptureSourceKind.Screen => new FullScreenCaptureDescriptor(),
        CaptureSourceKind.Monitor => new MonitorCaptureDescriptor(settings.MonitorId ?? "primary"),
        CaptureSourceKind.Window when !string.IsNullOrWhiteSpace(settings.WindowTitle) =>
            new WindowCaptureDescriptor(WindowTitle: settings.WindowTitle),
        CaptureSourceKind.Window => throw new CliUsageException("Window capture requires --title or a preset window title."),
        CaptureSourceKind.Region when settings.Region is { } region => new RegionCaptureDescriptor(region),
        CaptureSourceKind.Region => throw new CliUsageException("Region capture requires --rect or a preset region."),
        _ => throw new CliUsageException("Unsupported capture source."),
    };

    private static GifOutputFormat ParseGifFormat(string? value, string outputPath)
    {
        var format = value?.ToLowerInvariant() ?? Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".gif" => "gif",
            ".webp" => "webp",
            _ => string.Empty,
        };
        return format switch
        {
            "gif" => GifOutputFormat.Gif,
            "webp" => GifOutputFormat.AnimatedWebP,
            _ => throw new CliUsageException("GIF export format must be gif or webp."),
        };
    }

    private static string RequireInput(CliInvocation invocation)
    {
        var inputPath = invocation.Require("in");
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file was not found: {inputPath}", inputPath);
        }

        return inputPath;
    }

    private static string RequireSinglePositional(CliInvocation invocation, string message)
    {
        if (invocation.Positional.Count != 1)
        {
            throw new CliUsageException(message);
        }

        return invocation.Positional[0];
    }

    private static void EnsureNoPositional(CliInvocation invocation)
    {
        if (invocation.Positional.Count != 0)
        {
            throw new CliUsageException($"{invocation.Verb} {invocation.Action} does not accept positional arguments.");
        }
    }

    private static async Task WaitForStopAsync(TimeSpan? duration, CancellationToken cancellationToken)
    {
        if (duration is not null)
        {
            await Task.Delay(duration.Value, cancellationToken).ConfigureAwait(false);
            return;
        }

        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopped.TrySetResult();
        };
        Console.CancelKeyPress += handler;
        try
        {
            await stopped.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private async Task<CliExitCode> WriteSuccessAsync(
        CliInvocation invocation,
        string command,
        string? outputPath = null,
        RecordingPreset? preset = null,
        RecordingAiSuggestion? suggestion = null)
    {
        if (invocation.Json)
        {
            await WriteJsonAsync(new
            {
                success = true,
                command,
                outputPath,
                preset,
                title = suggestion?.Title,
                caption = suggestion?.Caption,
                aiFallback = suggestion?.IsFallback,
            }).ConfigureAwait(false);
        }
        else
        {
            await _output.WriteLineAsync(outputPath ?? $"{command}: ok").ConfigureAwait(false);
            if (suggestion is not null)
            {
                await _output.WriteLineAsync($"Suggested title: {suggestion.Title}").ConfigureAwait(false);
                await _output.WriteLineAsync($"Caption: {suggestion.Caption}").ConfigureAwait(false);
            }
        }

        return CliExitCode.Success;
    }

    private async Task<CliExitCode> WriteFailureAsync(
        CliInvocation invocation,
        string error,
        CliExitCode exitCode)
    {
        if (invocation.Json)
        {
            await WriteJsonAsync(new { success = false, command = invocation.Verb, error, exitCode = (int)exitCode })
                .ConfigureAwait(false);
        }
        else
        {
            await _output.WriteLineAsync($"error: {error}").ConfigureAwait(false);
        }

        return exitCode;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsAiService && _aiService is IDisposable disposableAiService)
        {
            disposableAiService.Dispose();
        }
    }

    private Task WriteJsonAsync<T>(T value) => _output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions));
}
