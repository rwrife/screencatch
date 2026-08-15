namespace ScreenCatch.Core.Capture;

public static class CaptureDescriptorValidator
{
    public static void Validate(CaptureSourceDescriptor descriptor, CaptureTopology topology)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(topology);

        switch (descriptor)
        {
            case FullScreenCaptureDescriptor:
                return;

            case MonitorCaptureDescriptor monitor:
                if (string.IsNullOrWhiteSpace(monitor.MonitorId))
                {
                    throw new CaptureValidationException("MonitorId is required for monitor capture.");
                }

                if (!topology.Monitors.ContainsKey(monitor.MonitorId))
                {
                    throw new CaptureValidationException($"Unknown monitor '{monitor.MonitorId}'.");
                }

                return;

            case WindowCaptureDescriptor window:
                if (string.IsNullOrWhiteSpace(window.WindowId) && string.IsNullOrWhiteSpace(window.WindowTitle))
                {
                    throw new CaptureValidationException("WindowId or WindowTitle is required for window capture.");
                }

                var hasWindow = topology.Windows.Any(w =>
                    (!string.IsNullOrWhiteSpace(window.WindowId) && string.Equals(w.Id, window.WindowId, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(window.WindowTitle) && string.Equals(w.Title, window.WindowTitle, StringComparison.OrdinalIgnoreCase)));

                if (!hasWindow)
                {
                    throw new CaptureValidationException("Requested window was not found in current topology.");
                }

                return;

            case RegionCaptureDescriptor region:
                if (!topology.VirtualDesktopBounds.Contains(region.Region))
                {
                    throw new CaptureValidationException("Region must be fully contained in the virtual desktop bounds.");
                }

                return;

            default:
                throw new CaptureValidationException($"Unsupported descriptor type: {descriptor.GetType().Name}");
        }
    }
}
