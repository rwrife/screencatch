namespace ScreenCatch.Core.Capture;

public static class CaptureGeometryResolver
{
    public static CaptureRect ResolveDescriptorRect(CaptureSourceDescriptor descriptor, CaptureTopology topology)
    {
        CaptureDescriptorValidator.Validate(descriptor, topology);

        return descriptor switch
        {
            FullScreenCaptureDescriptor => topology.VirtualDesktopBounds,
            MonitorCaptureDescriptor monitor => topology.Monitors[monitor.MonitorId],
            WindowCaptureDescriptor window => ResolveWindowRect(window, topology),
            RegionCaptureDescriptor region => region.Region,
            _ => throw new CaptureValidationException($"Unsupported descriptor type: {descriptor.GetType().Name}"),
        };
    }

    private static CaptureRect ResolveWindowRect(WindowCaptureDescriptor descriptor, CaptureTopology topology)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.WindowId))
        {
            var byId = topology.Windows.FirstOrDefault(w => string.Equals(w.Id, descriptor.WindowId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
            {
                return byId.Bounds;
            }
        }

        if (!string.IsNullOrWhiteSpace(descriptor.WindowTitle))
        {
            var byTitle = topology.Windows.FirstOrDefault(w => string.Equals(w.Title, descriptor.WindowTitle, StringComparison.OrdinalIgnoreCase));
            if (byTitle is not null)
            {
                return byTitle.Bounds;
            }
        }

        throw new CaptureValidationException("Requested window was not found in current topology.");
    }
}
