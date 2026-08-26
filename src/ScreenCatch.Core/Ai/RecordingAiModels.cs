using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Ai;

public sealed record RecordingAiRequest(
    DateTimeOffset RecordedAtUtc,
    TimeSpan Duration,
    CaptureSourceKind Source,
    string? PresetName = null,
    byte[]? SampleFramePng = null);

public sealed record RecordingAiSuggestion(string Title, string Caption, bool IsFallback);

public sealed record RecordingAiOptions(
    bool Enabled = false,
    string Endpoint = "http://localhost:11434/v1/",
    string Model = "qwen2.5:3b",
    TimeSpan? ProbeTimeout = null,
    TimeSpan? RequestTimeout = null);
