namespace ScreenCatch.Core.Capture;

/// <summary>
/// Integer rectangle in desktop-space pixels.
/// </summary>
public readonly record struct CaptureRect
{
    public CaptureRect(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(CaptureRect other)
    {
        return other.X >= X
            && other.Y >= Y
            && other.Right <= Right
            && other.Bottom <= Bottom;
    }

    public bool Intersects(CaptureRect other)
    {
        return X < other.Right
            && Right > other.X
            && Y < other.Bottom
            && Bottom > other.Y;
    }

    public CaptureRect Intersect(CaptureRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        if (left >= right || top >= bottom)
        {
            throw new InvalidOperationException("Rectangles do not overlap.");
        }

        return new CaptureRect(left, top, right - left, bottom - top);
    }
}
