namespace ScreenCatch.App.Services;

public sealed class CountdownService : ICountdownService
{
    public async Task RunAsync(int seconds, Action<int> tick, CancellationToken cancellationToken = default)
    {
        if (seconds is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Countdown must be between 0 and 10 seconds.");
        }

        for (var remaining = seconds; remaining > 0; remaining--)
        {
            tick(remaining);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}
