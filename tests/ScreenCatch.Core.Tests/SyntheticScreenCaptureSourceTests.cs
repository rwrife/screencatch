using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Tests;

public sealed class SyntheticScreenCaptureSourceTests
{
    [Fact]
    public async Task CaptureLoop_CollectsRequestedFrames_AndRaisesProgress()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var provider = new SyntheticFrameProvider(topology);
        await using var source = new SyntheticScreenCaptureSource(provider);

        var progressEvents = 0;
        source.Progress += (_, _) => progressEvents++;

        var request = new CaptureRequest(
            new MonitorCaptureDescriptor("primary"),
            targetFps: 20,
            maxFrames: 5);

        await source.StartAsync(request);

        for (var i = 0; i < 100 && source.IsCapturing; i++)
        {
            await Task.Delay(10);
        }

        var frames = await source.StopAsync();

        Assert.Equal(5, frames.Count);
        Assert.True(progressEvents >= 5);
        Assert.All(frames, frame =>
        {
            Assert.Equal(topology.Monitors["primary"], frame.Bounds);
            Assert.Equal(frame.Bounds.Width * 4, frame.Stride);
        });
    }

    [Fact]
    public async Task CancelAsync_StopsCaptureEarly()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var provider = new SyntheticFrameProvider(topology);
        await using var source = new SyntheticScreenCaptureSource(provider);

        var request = new CaptureRequest(
            new FullScreenCaptureDescriptor(),
            targetFps: 60,
            maxFrames: 200);

        await source.StartAsync(request);
        await Task.Delay(50);
        await source.CancelAsync();

        var frames = await source.StopAsync();

        Assert.True(frames.Count > 0);
        Assert.True(frames.Count < 200);
        Assert.False(source.IsCapturing);
    }
}
