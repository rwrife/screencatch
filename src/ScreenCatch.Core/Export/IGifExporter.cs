namespace ScreenCatch.Core.Export;

public interface IGifExporter
{
    long EstimateOutputSize(GifExportRequest request);

    Task<GifExportResult> ExportAsync(GifExportRequest request, CancellationToken cancellationToken = default);
}
