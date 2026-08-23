using ScreenCatch.Core.Editing;

namespace ScreenCatch.Core.Tests;

public sealed class TrimArgumentBuilderTests
{
    [Fact]
    public void Build_UsesStreamCopyForMatchingContainers()
    {
        var request = new TrimRequest(
            "/captures/source.mp4",
            "/captures/clip.mp4",
            new TrimRange(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(8)));

        var arguments = TrimArgumentBuilder.Build(request).ToArray();

        Assert.Contains("-ss", arguments);
        Assert.Contains("2.5", arguments);
        Assert.Contains("-t", arguments);
        Assert.Contains("5.5", arguments);
        Assert.Contains("-c", arguments);
        Assert.Contains("copy", arguments);
        Assert.Equal(new[] { "0:v:0", "0:a:0?" }, MappedStreams(arguments));
        Assert.Equal("/captures/clip.mp4", arguments[^1]);
    }

    [Fact]
    public void Build_ReencodesWhenChangingContainers()
    {
        var request = new TrimRequest(
            "/captures/source.mp4",
            "/captures/clip.webm",
            new TrimRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)));

        var arguments = TrimArgumentBuilder.Build(request).ToArray();

        Assert.DoesNotContain("copy", arguments);
        Assert.Contains("libvpx-vp9", arguments);
        Assert.Contains("libopus", arguments);
    }

    [Fact]
    public void Build_MapsOnlyFirstVideoAndOptionalAudioWhenReencoding()
    {
        var request = new TrimRequest(
            "/captures/source.mkv",
            "/captures/clip.mp4",
            new TrimRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)));

        var arguments = TrimArgumentBuilder.Build(request).ToArray();
        var mappedStreams = MappedStreams(arguments);

        Assert.Equal(new[] { "0:v:0", "0:a:0?" }, mappedStreams);
        Assert.DoesNotContain("0", mappedStreams);
    }

    [Fact]
    public void Build_ReencodesForFrameAccurateCut()
    {
        var request = new TrimRequest(
            "/captures/source.mp4",
            "/captures/clip.mp4",
            new TrimRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)),
            requireFrameAccurateCut: true);

        var arguments = TrimArgumentBuilder.Build(request).ToArray();

        Assert.DoesNotContain("copy", arguments);
        Assert.Contains("libx264", arguments);
        Assert.Contains("aac", arguments);
    }

    private static IEnumerable<string> MappedStreams(string[] arguments) => arguments
        .Select((argument, index) => (argument, index))
        .Where(item => item.argument == "-map")
        .Select(item => arguments[item.index + 1]);
}
