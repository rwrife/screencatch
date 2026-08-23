namespace ScreenCatch.Core.Editing;

public interface ITrimmer
{
    Task<TrimResult> TrimAsync(TrimRequest request, CancellationToken cancellationToken = default);
}
