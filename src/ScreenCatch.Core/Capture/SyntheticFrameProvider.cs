namespace ScreenCatch.Core.Capture;

/// <summary>
/// Deterministic in-memory frame provider used for capture pipeline validation and harnessing.
/// </summary>
public sealed class SyntheticFrameProvider : IFrameProvider
{
    public SyntheticFrameProvider(CaptureTopology topology)
    {
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
    }

    public CaptureTopology Topology { get; }

    public CaptureFrame CaptureFrame(int frameIndex, DateTimeOffset capturedAtUtc)
    {
        if (frameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var desktop = Topology.VirtualDesktopBounds;
        var stride = desktop.Width * 4;
        var buffer = new byte[stride * desktop.Height];

        for (var y = 0; y < desktop.Height; y++)
        {
            for (var x = 0; x < desktop.Width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var globalX = desktop.X + x;
                var globalY = desktop.Y + y;

                buffer[offset + 0] = (byte)(globalX & 0xFF); // B
                buffer[offset + 1] = (byte)(globalY & 0xFF); // G
                buffer[offset + 2] = (byte)(frameIndex & 0xFF); // R
                buffer[offset + 3] = 255; // A
            }
        }

        return new CaptureFrame(capturedAtUtc, desktop, stride, buffer);
    }
}
