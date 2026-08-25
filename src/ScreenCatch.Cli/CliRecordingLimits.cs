namespace ScreenCatch.Cli;

public static class CliRecordingLimits
{
    public static int GetMaxFrames(TimeSpan? duration, int framesPerSecond)
    {
        if (duration is null)
        {
            return int.MaxValue;
        }

        var frames = Math.Ceiling(duration.Value.TotalSeconds * framesPerSecond);
        return frames >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)frames);
    }
}
