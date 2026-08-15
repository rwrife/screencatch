using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Tests;

public sealed class CaptureDescriptorValidatorTests
{
    [Fact]
    public void Validate_Throws_ForUnknownMonitor()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var descriptor = new MonitorCaptureDescriptor("missing-monitor");

        Assert.Throws<CaptureValidationException>(() =>
            CaptureDescriptorValidator.Validate(descriptor, topology));
    }

    [Fact]
    public void Validate_Passes_ForKnownWindowByTitle()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var descriptor = new WindowCaptureDescriptor(WindowTitle: "Terminal");

        CaptureDescriptorValidator.Validate(descriptor, topology);
    }

    [Fact]
    public void Validate_Throws_ForRegionOutsideDesktop()
    {
        var topology = CaptureTopology.CreateDefaultForTests();
        var descriptor = new RegionCaptureDescriptor(new CaptureRect(1900, 1000, 50, 50));

        Assert.Throws<CaptureValidationException>(() =>
            CaptureDescriptorValidator.Validate(descriptor, topology));
    }
}
