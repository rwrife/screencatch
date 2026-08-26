namespace ScreenCatch.Core.Ai;

public interface IRecordingAiService
{
    Task<bool> ProbeAsync(
        RecordingAiOptions options,
        CancellationToken cancellationToken = default);

    Task<RecordingAiSuggestion> SuggestAsync(
        RecordingAiRequest request,
        RecordingAiOptions options,
        CancellationToken cancellationToken = default);
}
