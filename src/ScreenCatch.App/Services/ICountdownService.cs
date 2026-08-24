namespace ScreenCatch.App.Services;

public interface ICountdownService
{
    Task RunAsync(int seconds, Action<int> tick, CancellationToken cancellationToken = default);
}
