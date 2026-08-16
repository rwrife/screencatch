namespace ScreenCatch.Core.Capture;

/// <summary>
/// A single BGRA frame with geometry metadata.
/// </summary>
public sealed class CaptureFrame
{
    public CaptureFrame(DateTimeOffset capturedAtUtc, CaptureRect bounds, int stride, byte[] buffer)
    {
        if (stride < bounds.Width * 4)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), "Stride must be >= width * 4 for BGRA frames.");
        }

        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Length != stride * bounds.Height)
        {
            throw new ArgumentException("Buffer size does not match frame dimensions and stride.", nameof(buffer));
        }

        CapturedAtUtc = capturedAtUtc;
        Bounds = bounds;
        Stride = stride;
        Buffer = buffer;
    }

    public DateTimeOffset CapturedAtUtc { get; }

    public CaptureRect Bounds { get; }

    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    public int Stride { get; }

    /// <summary>
    /// Raw BGRA pixels. Length = <see cref="Stride"/> * <see cref="Height"/>.
    /// </summary>
    public byte[] Buffer { get; }
}
