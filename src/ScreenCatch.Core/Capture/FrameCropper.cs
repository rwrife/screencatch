namespace ScreenCatch.Core.Capture;

public static class FrameCropper
{
    /// <summary>
    /// Crop a BGRA frame to <paramref name="cropRect"/>.
    /// </summary>
    public static CaptureFrame CropFrame(CaptureFrame inputFrame, CaptureRect cropRect)
    {
        ArgumentNullException.ThrowIfNull(inputFrame);

        if (!inputFrame.Bounds.Contains(cropRect))
        {
            throw new CaptureValidationException("Crop rectangle must be fully contained in source frame bounds.");
        }

        var bytesPerPixel = 4;
        var outputStride = cropRect.Width * bytesPerPixel;
        var output = new byte[outputStride * cropRect.Height];

        var sourceOffsetX = cropRect.X - inputFrame.Bounds.X;
        var sourceOffsetY = cropRect.Y - inputFrame.Bounds.Y;

        for (var y = 0; y < cropRect.Height; y++)
        {
            var sourceRow = ((sourceOffsetY + y) * inputFrame.Stride) + (sourceOffsetX * bytesPerPixel);
            var destinationRow = y * outputStride;
            Buffer.BlockCopy(inputFrame.Buffer, sourceRow, output, destinationRow, outputStride);
        }

        return new CaptureFrame(inputFrame.CapturedAtUtc, cropRect, outputStride, output);
    }
}
