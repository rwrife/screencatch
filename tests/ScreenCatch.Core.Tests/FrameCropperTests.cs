using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Tests;

public sealed class FrameCropperTests
{
    [Fact]
    public void CropFrame_CropsExpectedPixels()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var provider = new SyntheticFrameProvider(topology);
        var frame = provider.CaptureFrame(frameIndex: 3, capturedAtUtc: DateTimeOffset.UtcNow);

        var cropRect = new CaptureRect(10, 20, 4, 3);
        var cropped = FrameCropper.CropFrame(frame, cropRect);

        Assert.Equal(cropRect, cropped.Bounds);
        Assert.Equal(4 * 4, cropped.Stride);
        Assert.Equal(cropped.Stride * 3, cropped.Buffer.Length);

        // Top-left pixel: B=globalX, G=globalY, R=frameIndex, A=255.
        Assert.Equal((byte)10, cropped.Buffer[0]);
        Assert.Equal((byte)20, cropped.Buffer[1]);
        Assert.Equal((byte)3, cropped.Buffer[2]);
        Assert.Equal((byte)255, cropped.Buffer[3]);

        // Bottom-right pixel at (x=13, y=22).
        var lastPixel = cropped.Buffer.Length - 4;
        Assert.Equal((byte)13, cropped.Buffer[lastPixel + 0]);
        Assert.Equal((byte)22, cropped.Buffer[lastPixel + 1]);
        Assert.Equal((byte)3, cropped.Buffer[lastPixel + 2]);
        Assert.Equal((byte)255, cropped.Buffer[lastPixel + 3]);
    }
}
