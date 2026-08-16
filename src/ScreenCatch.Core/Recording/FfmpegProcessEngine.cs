using System.Diagnostics;

namespace ScreenCatch.Core.Recording;

public sealed class FfmpegProcessEngine : IFfmpegEngine
{
    private readonly string _ffmpegPath;

    public FfmpegProcessEngine(string ffmpegPath = "ffmpeg")
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            throw new ArgumentException("ffmpeg path is required.", nameof(ffmpegPath));
        }

        _ffmpegPath = ffmpegPath;
    }

    public async Task<FfmpegProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task> writeStandardInputAsync,
        Action<string>? onStandardErrorLine = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(writeStandardInputAsync);

        var stderrLines = new List<string>();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var stderrTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                stderrLines.Add(line);
                onStandardErrorLine?.Invoke(line);
            }
        }, CancellationToken.None);

        try
        {
            await writeStandardInputAsync(process.StandardInput.BaseStream, cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await stderrTask.ConfigureAwait(false);

            return new FfmpegProcessResult(process.ExitCode, stderrLines);
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort only
        }
    }
}
