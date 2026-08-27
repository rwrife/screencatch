using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class MediaToolLocatorTests
{
    [Fact]
    public void Resolve_PrefersBundledToolsDirectory()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var rootTool = Path.Combine(directory, executableName);
            var bundledTool = Path.Combine(directory, "tools", executableName);
            Directory.CreateDirectory(Path.GetDirectoryName(bundledTool)!);
            File.WriteAllText(rootTool, string.Empty);
            File.WriteAllText(bundledTool, string.Empty);

            Assert.Equal(bundledTool, MediaToolLocator.Resolve("ffmpeg", directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_FallsBackToPathLookupName()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var expected = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
            Assert.Equal(expected, MediaToolLocator.Resolve("ffprobe", directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_RejectsPaths()
    {
        Assert.Throws<ArgumentException>(() => MediaToolLocator.Resolve(Path.Combine("tools", "ffmpeg")));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"screencatch-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
