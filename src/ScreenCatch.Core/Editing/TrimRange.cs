namespace ScreenCatch.Core.Editing;

public readonly record struct TrimRange
{
    public TrimRange(TimeSpan start, TimeSpan end)
    {
        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The in point cannot be negative.");
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The out point must be after the in point.");
        }

        Start = start;
        End = end;
    }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public TimeSpan Duration => End - Start;
}
