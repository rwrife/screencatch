using System.ComponentModel;
using System.Text.Json;

namespace ScreenCatch.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var json = args.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        try
        {
            var invocation = CliParser.Parse(args);
            var ffmpegPath = invocation.Get("ffmpeg") ?? "ffmpeg";
            var ffprobePath = invocation.Get("ffprobe") ?? "ffprobe";
            using var application = new CliApplication(
                Console.Out,
                ffmpegEngine: new Core.Recording.FfmpegProcessEngine(ffmpegPath),
                mediaProbe: new FfprobeMediaProbe(ffprobePath));
            return (int)await application.RunAsync(invocation).ConfigureAwait(false);
        }
        catch (CliUsageException exception)
        {
            return await WriteErrorAsync(json, exception.Message, CliExitCode.Usage).ConfigureAwait(false);
        }
        catch (FileNotFoundException exception)
        {
            return await WriteErrorAsync(json, exception.Message, CliExitCode.InputNotFound).ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            return await WriteErrorAsync(json, exception.Message, CliExitCode.ToolUnavailable).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await WriteErrorAsync(json, "Operation canceled.", CliExitCode.Canceled).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            return await WriteErrorAsync(json, exception.Message, CliExitCode.OperationFailed).ConfigureAwait(false);
        }
    }

    private static async Task<int> WriteErrorAsync(bool json, string message, CliExitCode exitCode)
    {
        if (json)
        {
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
            {
                success = false,
                error = message,
                exitCode = (int)exitCode,
            })).ConfigureAwait(false);
        }
        else
        {
            await Console.Error.WriteLineAsync($"error: {message}").ConfigureAwait(false);
        }

        return (int)exitCode;
    }
}
