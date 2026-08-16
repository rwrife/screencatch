# ScreenCatch.Core capture bootstrap

This directory contains the first UI-free capture core for issue #1.

## What is implemented

- `IScreenCaptureSource` with `StartAsync`, `StopAsync`, `CancelAsync`, and per-frame `Progress` events.
- Source descriptors:
  - `FullScreenCaptureDescriptor`
  - `MonitorCaptureDescriptor`
  - `WindowCaptureDescriptor`
  - `RegionCaptureDescriptor`
- Geometry models:
  - `CaptureRect` (bounds + math)
  - `CaptureFrame` (timestamped BGRA frame with stride)
  - `CaptureTopology` (virtual desktop + monitor/window inventories)
- Validation + resolution:
  - `CaptureDescriptorValidator`
  - `CaptureGeometryResolver`
  - `FrameCropper`
- One working in-memory capture implementation:
  - `SyntheticFrameProvider`
  - `SyntheticScreenCaptureSource`

## Platform backends

`WindowsScreenCaptureSource` and `MacScreenCaptureSource` are included as
bootstrap classes and currently delegate to the synthetic provider. They are
intended to be replaced by native capture backends in follow-up work:

- Windows: `Windows.Graphics.Capture` / DXGI Desktop Duplication
- macOS: `ScreenCaptureKit` (fallback `AVFoundation`)

## Manual harness workflow (developer)

Use `CaptureTopology.CreateDefaultForTests()` + `ScreenCaptureSourceFactory`
to run short N-frame capture loops while wiring native backends.

For deterministic verification, synthetic frames encode pixel values as:

- `B = globalX`
- `G = globalY`
- `R = frameIndex`
- `A = 255`

This allows precise assertions for crop correctness.
