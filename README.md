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

# Use a saved preset
screencatch record --preset "issue-repro" --out repro.mp4
```

## Local-AI integration (optional, off by default)

ScreenCatch can *optionally* use a **local** tiny model to suggest an **auto-title** or short **caption** for a recording (handy for naming files or drafting an issue note). It talks to an **OpenAI-compatible localhost endpoint** (e.g. **Ollama** or **llama.cpp**) using small models in the **Llama 3.2 / Qwen2.5 / Phi-3-mini / MiniCPM-V** class. It:

- is **disabled by default** and only ever contacts `localhost`;
- performs a **reachability probe** and **gracefully falls back** to timestamp/preset-based naming when no model is present;
- sends only minimal metadata (and, for vision models, optionally a single sampled frame) — **never uploads to any cloud**.

## Current status / milestones

🚧 **Early scaffolding.** Docs and backlog are in place; implementation is issue-by-issue.

- [ ] M1 — Core capture + encode engine (region/window/screen → MP4/WebM)
- [ ] M2 — GIF/animated-WebP export (two-pass palette) + trim
- [ ] M3 — Desktop UI (source picker, recording HUD, countdown, cursor/click effects)
- [ ] M4 — CLI + JSON presets shared with GUI
- [ ] M5 — Optional local-AI auto-title/caption
- [ ] M6 — Packaging & CI (Windows zip/MSIX, macOS .app/.dmg)

See `PLAN.md` for scope, architecture, and non-goals.
