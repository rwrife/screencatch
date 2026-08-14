# ScreenCatch — Plan

## Scope

A small, privacy-first desktop **screen recorder & GIF studio** for **Windows 10/11** and **macOS**.

**In scope**

- Capture sources: full screen, specific monitor, single window, rubber-band region.
- Video recording: H.264 **MP4** and VP9 **WebM**, selectable FPS & quality, optional system/mic audio.
- GIF / animated-**WebP** export via two-pass `palettegen`/`paletteuse` with size/FPS controls + estimated output size.
- Recording aids: pre-roll countdown, cursor highlight ring, click-ripple effects.
- Simple trim (in/out) and re-export.
- Named **JSON presets** shared between GUI and CLI.
- Headless `screencatch` CLI mirroring the GUI engine.
- Optional, off-by-default **local-AI** auto-title/caption (localhost only).

**Non-goals** (see below).

## Architecture / tech approach

- **Runtime:** .NET 8.
- **UI:** **Avalonia (MVVM)** for a single cross-platform desktop app. (WPF rejected — Windows-only.)
- **UI-free core — `ScreenCatch.Core`:** all logic behind interfaces so it is testable and reused by GUI + CLI:
  - `IScreenCaptureSource` — per-OS frame grabbers: Windows **Windows.Graphics.Capture** / DXGI Desktop Duplication; macOS **ScreenCaptureKit** (fallback **AVFoundation**). Emits timestamped frames + geometry for screen/monitor/window/region.
  - `ICursorOverlay` — composites cursor highlight ring + click-ripple effects onto frames.
  - `IVideoEncoder` — encodes frames (+ optional audio) to MP4/WebM. Backed by an FFmpeg process wrapper (`IFfmpegEngine`, arg-array only — no shell string interpolation) with progress/cancel; native encoder path evaluated later.
  - `IGifExporter` — two-pass palettegen/paletteuse GIF + animated-WebP, FPS/size controls, estimated size.
  - `ITrimmer` — in/out trimming, stream-copy where possible.
  - `IAudioCapture` — optional system-loopback (WASAPI loopback / macOS aggregate device) + mic.
  - `IRecordingSession` — orchestrates source → overlay → encoder with countdown, pause/resume, cancel.
  - `IPresetStore` — JSON presets/settings under `%APPDATA%\screencatch` / `~/Library/Application Support/screencatch`.
  - `IRecordingAiService` — optional local-AI auto-title/caption (see below).
- **CLI — `ScreenCatch.Cli`:** verbs `record`, `gif`, `trim`, `probe`, `preset`; `--json` output and script-friendly exit codes.
- **Local-AI:** `IRecordingAiService` → OpenAI-compatible **localhost** endpoint (Ollama / llama.cpp), small **Llama 3.2 / Qwen2.5 / Phi-3-mini / MiniCPM-V** class models. Reachability probe + deterministic fallback (timestamp/preset naming). Off by default, local-only, minimal metadata (optionally one sampled frame for vision).
- **Testing:** xUnit against `ScreenCatch.Core` (encode arg-building, GIF palette pipeline, trim math, preset round-trip, fallback logic) with the FFmpeg/native layers mocked behind interfaces.

## Milestones

1. **M1 — Capture + encode:** `IScreenCaptureSource` (screen/monitor/window/region) + `IVideoEncoder` → MP4/WebM with FPS/quality; `IRecordingSession` start/stop/cancel.
2. **M2 — GIF + trim:** `IGifExporter` two-pass palette GIF/animated-WebP + `ITrimmer`, estimated size.
3. **M3 — Desktop UI:** Avalonia source picker, region selector overlay, recording HUD, countdown, cursor highlight + click effects, output preview.
4. **M4 — CLI + presets:** `screencatch` verbs + `IPresetStore` JSON presets shared GUI↔CLI.
5. **M5 — Local-AI:** optional auto-title/caption with probe + graceful fallback, off by default.
6. **M6 — Packaging & CI:** Windows portable zip + MSIX, macOS universal `.app`/`.dmg`, GitHub Actions matrix (windows-latest + macos-latest), bundle per-OS FFmpeg where needed.

## Non-goals

- No cloud upload, accounts, hosting, or telemetry.
- No full non-linear video editor or multi-track timeline (that overlaps `reelpress`); ScreenCatch does *capture* + light trim only.
- No static screenshot annotation (that is `markup-shot`).
- No webcam-compositing / streaming / live broadcasting.
- No mobile or Linux targets in the initial scope.

## Packaging / distribution target

- **Windows 10/11:** self-contained `win-x64` portable **zip** + **MSIX** installer.
- **macOS:** **universal** (`arm64` + `x64`) `.app` bundled into a **.dmg**; document Screen Recording / Microphone permission prompts.
- **CI:** GitHub Actions build/test matrix on `windows-latest` and `macos-latest`; bundle FFmpeg/ffprobe per OS where used.
