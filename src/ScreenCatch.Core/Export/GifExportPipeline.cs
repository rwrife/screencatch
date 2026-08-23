namespace ScreenCatch.Core.Export;

public sealed record GifExportPipeline(
    IReadOnlyList<string> PaletteArguments,
    IReadOnlyList<string> ExportArguments);
