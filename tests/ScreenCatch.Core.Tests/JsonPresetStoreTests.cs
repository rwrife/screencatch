using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Presets;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.Core.Tests;

public sealed class JsonPresetStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"screencatch-presets-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSharedRecordingSettings()
    {
        var store = new JsonPresetStore(_directory);
        var preset = new RecordingPreset(
            "issue-repro",
            CaptureSourceKind.Region,
            FramesPerSecond: 15,
            Format: VideoOutputFormat.WebM,
            Quality: 31,
            Region: new CaptureRect(10, 20, 960, 540),
            Audio: PresetAudioMode.Microphone);

        await store.SaveAsync(preset);
        var loaded = await store.LoadAsync("issue-repro");

        Assert.Equal(preset, loaded);
        Assert.Contains("\"name\": \"issue-repro\"", await File.ReadAllTextAsync(Path.Combine(_directory, "issue-repro.json")));
    }

    [Fact]
    public async Task ListAsync_ReturnsPresetNamesInOrdinalOrder()
    {
        var store = new JsonPresetStore(_directory);
        await store.SaveAsync(new RecordingPreset("zebra"));
        await store.SaveAsync(new RecordingPreset("alpha"));

        var names = await store.ListAsync();

        Assert.Equal(["alpha", "zebra"], names);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with/slash")]
    [InlineData(" ")]
    public async Task SaveAsync_RejectsUnsafeNames(string name)
    {
        var store = new JsonPresetStore(_directory);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new RecordingPreset(name)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
