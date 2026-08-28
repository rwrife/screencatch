# ScreenCatch

Local, privacy-first **screen recorder & GIF studio** for **Windows 10/11** and **macOS**. Capture a region, a window, or a full screen to **MP4/WebM**, or turn any capture into an **optimized GIF / animated-WebP** — with a countdown, cursor highlight & click effects, quick trimming, and reusable presets. Everything runs **offline**; there is **no cloud dependency**.

> The capture-and-record companion to the tool-lab family: where `markup-shot` annotates a *static* screenshot and `reelpress` processes *existing* video files, **ScreenCatch records your screen in the first place** and exports lightweight shareable clips.

## Overview

ScreenCatch is a small desktop app (plus a headless CLI) built around a clean, UI-free core:

- **Capture sources:** full screen, a chosen monitor, a single window, or a rubber-band region.
- **Record to video:** H.264 MP4 or VP9 WebM, selectable FPS and quality, optional system/mic audio.
- **Record to GIF:** two-pass palettegen/paletteuse GIF or animated-WebP with size/FPS controls and a live estimated output size.
- **Polish while you record:** pre-roll countdown, cursor highlight ring, and click-ripple effects to make demos readable.
- **Trim & export:** mark in/out points and export without a full re-encode where possible.
- **Presets:** save capture + encode settings as named JSON presets, shared between the GUI and CLI.

## Motivation

Recording a quick demo, bug repro, or tutorial clip usually means reaching for a heavyweight suite or a cloud uploader that phones home. ScreenCatch keeps it **simple, local, and private**: a fast recorder that produces small MP4s and tidy GIFs you can drop straight into an issue, chat, or doc — no account, no upload, no telemetry.

## Use cases

- Capture a **bug repro** clip to attach to a GitHub issue.
- Record a short **how-to / tutorial** segment for docs.
- Make a **GIF** of a UI interaction for a README or PR.
- Grab a **window-only** recording of one app without desktop clutter.
- Produce **consistent clips** across a team via shared presets.

## How to use

### Windows 10/11 quickstart

1. Download the latest `screencatch-win-x64.zip` from Releases (or build from source — see `PLAN.md`), unzip, and run `ScreenCatch.exe`. An MSIX installer is also planned.
2. Pick a **capture source** (screen / monitor / window / region).
3. Choose **Video (MP4/WebM)** or **GIF/WebP**, set FPS & quality.
4. Press **Record** (a countdown runs), do your thing, press **Stop**.
5. Optionally **trim**, then **Save** or **Copy** the result.

### macOS quickstart

1. Download `ScreenCatch-macOS-universal.dmg` from Releases (or build from source), open it, and drag **ScreenCatch** to Applications.
2. On first launch, grant **Screen Recording** permission (System Settings → Privacy & Security → Screen Recording) and, if recording the mic, **Microphone** permission.
3. Pick a capture source, choose output format, and record exactly as above.

### Packaging from source

The GitHub Actions `CI` workflow builds and tests on both `windows-latest` and
`macos-latest`, then uploads installable artifacts. Both packages include
`ffmpeg` and `ffprobe` under the application's `tools` directory; ScreenCatch
prefers those binaries and falls back to `PATH` for source builds.

- **Windows:** run `./scripts/package-windows.ps1` in PowerShell on Windows.
  It creates `artifacts/windows/screencatch-win-x64.zip`, a self-contained
  `screencatch-win-x64.msix`, and its development signing certificate. For a
  CI-built MSIX, import `screencatch-dev-signing.cer` into the local machine's
  **Trusted People** store before installation. Release builds should replace
  this ephemeral development signature with a trusted code-signing identity.
- **macOS:** run `./scripts/package-macos.sh` on macOS. It publishes both
  `osx-x64` and `osx-arm64`, combines their Mach-O binaries into a universal
  app, and creates `artifacts/macos/screencatch-macOS-universal.dmg`. On first
  launch, macOS prompts for **Screen Recording** permission and, when
  microphone capture is used, **Microphone** permission under System Settings
  → Privacy & Security. With no signing configuration, the script creates an
  ad-hoc signed **development artifact**; use Finder's **Open** context-menu
  action to approve it. For normal Gatekeeper distribution, set
  `MACOS_CODESIGN_IDENTITY` to a Developer ID Application identity and
  `MACOS_NOTARY_PROFILE` to an `xcrun notarytool` keychain profile; the script
  enables the hardened runtime with the CoreCLR JIT entitlements, notarizes
  the DMG, and staples its ticket.

Packaging scripts use versioned FFmpeg/ffprobe archives and verify committed
SHA-256 digests before including any downloaded executable.

## Example workflow / commands

Headless CLI (same engine as the GUI):

```bash
# Record the primary screen to MP4 at 30fps for a demo
screencatch record --source screen --fps 30 --format mp4 --out demo.mp4

# Record a region to an optimized GIF (two-pass palette)
screencatch record --source region --rect 100,100,960,540 --format gif --fps 15 --out ui.gif

# Record a specific window with mic audio
screencatch record --source window --title "My App" --audio mic --format webm --out walkthrough.webm

# Trim an existing capture and re-export a GIF
screencatch gif --in demo.mp4 --start 00:00:02 --end 00:00:08 --fps 12 --out clip.gif

# Use optional local AI for a suggested title/caption (loopback only; off by default)
screencatch record --source region --rect 100,100,960,540 --format mp4 \
  --duration 10 --out demo.mp4 --ai \
  --ai-endpoint http://localhost:11434/v1/ --ai-model qwen2.5:3b --json

# Use a saved preset
screencatch record --preset "issue-repro" --out repro.mp4

# Save/list a preset shared with the desktop app
screencatch preset save issue-repro --source region --rect 100,100,960,540 --fps 15 --format mp4
screencatch preset list --json
```

`record` runs until Ctrl+C unless `--duration SECONDS` is supplied. Every verb
supports `--json` for machine-readable output. Script-friendly exit codes are:
`0` success, `2` usage, `3` missing input, `4` FFmpeg/ffprobe unavailable,
`5` operation failure, and `130` cancellation. Named presets are stored as JSON
under `%APPDATA%\\screencatch\\presets` on Windows and
`~/Library/Application Support/screencatch/presets` on macOS; the CLI and
Avalonia view model use the same `IPresetStore` contract.

## Local-AI integration (optional, off by default)

ScreenCatch can *optionally* use a **local** tiny model to suggest an **auto-title** or short **caption** for a recording (handy for naming files or drafting an issue note). It talks to an **OpenAI-compatible localhost endpoint** (e.g. **Ollama** or **llama.cpp**) using small models in the **Llama 3.2 / Qwen2.5 / Phi-3-mini / MiniCPM-V** class. Enable it with the desktop **Local AI** checkbox or the CLI `--ai` flag; both are off by default. It:

- is **disabled by default** and rejects any endpoint that is not HTTP(S) loopback (`localhost`, `127.0.0.1`, or `::1`);
- probes `<endpoint>/models` before requesting `<endpoint>/chat/completions` and **gracefully falls back** to timestamp/preset-based naming when no model is present or a response is invalid;
- sends only recording time, duration, source type, optional preset name, and (for vision models) at most one optional inline PNG frame — **never uploads to any cloud**;
- returns `title`, `caption`, and `aiFallback` in CLI JSON output when `--ai` is enabled.

## Current status / milestones

🚧 **In active implementation.** The UI-free core covers capture, MP4/WebM encoding, optimized two-pass GIF/animated-WebP export, output-size estimation, stream-copy/frame-accurate trimming, cursor/click compositing, and shared JSON presets. The headless CLI now provides record/GIF/trim/probe/preset verbs with stable JSON output, while the Avalonia desktop app provides source and region selection, recording controls, countdown, format/FPS/quality settings, and output save/copy actions. Native Windows/macOS frame grabbers and higher milestones remain in progress.

- [x] M1 — Core capture + encode engine (region/window/screen → MP4/WebM)
- [x] M2 — GIF/animated-WebP export (two-pass palette) + trim
- [x] M3 — Desktop UI (source picker, recording HUD, countdown, cursor/click effects)
- [x] M4 — CLI + JSON presets shared with GUI
- [x] M5 — Optional local-AI auto-title/caption
- [x] M6 — Packaging & CI (Windows zip/MSIX, macOS .app/.dmg)

See `PLAN.md` for scope, architecture, and non-goals.
