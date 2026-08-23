namespace ScreenCatch.Core.Export;

public static class GifExportSizeEstimator
{
    public static long EstimateBytes(GifExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var height = MakeEven((int)Math.Round(
            request.Width * (double)request.SourceHeight / request.SourceWidth));
        var frameCount = request.EffectiveRange.Duration.TotalSeconds * request.FramesPerSecond;
        var compressedBytesPerPixel = request.Format == GifOutputFormat.Gif ? 0.15d : 0.08d;

        return Math.Max(1, (long)Math.Ceiling(
            request.Width * (double)height * frameCount * compressedBytesPerPixel));
    }

    private static int MakeEven(int value) => Math.Max(2, value - (value % 2));
}
