#!/usr/bin/env bash
set -euo pipefail

DESTINATION="${1:?usage: install-ffmpeg-macos.sh DESTINATION [native|universal]}"
MODE="${2:-native}"
case "$MODE" in
  native|universal) ;;
  *) echo "mode must be native or universal" >&2; exit 2 ;;
esac
mkdir -p "$DESTINATION"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/screencatch-ffmpeg-XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

download_verified() {
  local url="$1"
  local expected_sha256="$2"
  local destination="$3"
  curl --fail --location --retry 3 "$url" --output "$destination"
  local actual_sha256
  actual_sha256="$(shasum -a 256 "$destination" | cut -d ' ' -f 1)"
  if [[ "$actual_sha256" != "$expected_sha256" ]]; then
    echo "Checksum mismatch for $url: expected $expected_sha256, got $actual_sha256" >&2
    exit 1
  fi
}

install_architecture() {
  local tool="$1"
  local architecture="$2"
  local destination="$3"
  local url sha256
  if [[ "$tool/$architecture" == "ffmpeg/x86_64" ]]; then
    url="https://evermeet.cx/ffmpeg/ffmpeg-8.1.2.zip"
    sha256="e91df72a1ee7c26606f90dd2dd4dcccc6a75140ff9ea6fdd50faae828b82ba69"
  elif [[ "$tool/$architecture" == "ffprobe/x86_64" ]]; then
    url="https://evermeet.cx/ffmpeg/ffprobe-8.1.2.zip"
    sha256="399b93f0b9862f69767afa343e90c2f48d7e7958cadbb6deb76a012d0e3b7ce3"
  elif [[ "$tool/$architecture" == "ffmpeg/arm64" ]]; then
    url="https://github.com/charlienovember/videobot-ffmpeg/releases/download/ffmpeg-n8.1.2/ffmpeg-n8.1.2-macos-arm64.zip"
    sha256="7c3f9560d1d018746a3fbca4f89d597ad4d45739abc3efc45f6360c9add3e614"
  else
    url="https://github.com/charlienovember/videobot-ffmpeg/releases/download/ffmpeg-n8.1.2/ffprobe-n8.1.2-macos-arm64.zip"
    sha256="d86c980604ab0d8c40b3c17c9c3fcb388fb341ea4bd470ab28f9d96c5f3fb812"
  fi

  local archive="$WORK/$tool-$architecture.zip"
  local extract="$WORK/$tool-$architecture"
  mkdir -p "$extract"
  download_verified "$url" "$sha256" "$archive"
  ditto -x -k "$archive" "$extract"
  local binary
  binary="$(find "$extract" -type f -name "$tool" -print -quit)"
  if [[ -z "$binary" ]]; then
    echo "Downloaded archive did not contain $tool for $architecture" >&2
    exit 1
  fi
  cp "$binary" "$destination"
  chmod 755 "$destination"
}

for tool in ffmpeg ffprobe; do
  if [[ "$MODE" == "universal" ]]; then
    install_architecture "$tool" x86_64 "$WORK/$tool-x86_64"
    install_architecture "$tool" arm64 "$WORK/$tool-arm64"
    lipo -create "$WORK/$tool-x86_64" "$WORK/$tool-arm64" -output "$DESTINATION/$tool"
    lipo -archs "$DESTINATION/$tool" | grep -q x86_64
    lipo -archs "$DESTINATION/$tool" | grep -q arm64
    chmod 755 "$DESTINATION/$tool"
  else
    architecture="$(uname -m)"
    case "$architecture" in
      x86_64|arm64) ;;
      *) echo "Unsupported macOS architecture: $architecture" >&2; exit 1 ;;
    esac
    install_architecture "$tool" "$architecture" "$DESTINATION/$tool"
  fi
done
