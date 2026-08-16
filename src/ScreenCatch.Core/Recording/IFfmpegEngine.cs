namespace ScreenCatch.Core.Recording;

public interface IFfmpegEngine
{
    Task<FfmpegProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task> writeStandardInputAsync,
        Action<string>? onStandardErrorLine = null,
        CancellationToken cancellationToken = default);
}
