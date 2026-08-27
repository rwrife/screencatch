#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT="${OUTPUT_DIRECTORY:-artifacts/macos}"
if [[ "$OUTPUT" != /* ]]; then
  OUTPUT="$ROOT/$OUTPUT"
fi
mkdir -p "$(dirname "$OUTPUT")"
OUTPUT="$(cd "$(dirname "$OUTPUT")" && pwd -P)/$(basename "$OUTPUT")"
case "$OUTPUT" in
  "$ROOT/artifacts/"*) ;;
  *)
    echo "OUTPUT_DIRECTORY must resolve to a child of $ROOT/artifacts" >&2
    exit 1
    ;;
esac
WORK="$OUTPUT/work"
X64="$WORK/osx-x64"
ARM64="$WORK/osx-arm64"
UNIVERSAL="$WORK/universal"
APP="$WORK/ScreenCatch.app"
CONTENTS="$APP/Contents"
MACOS="$CONTENTS/MacOS"
TOOLS="$MACOS/tools"

rm -rf "$OUTPUT"
mkdir -p "$X64" "$ARM64" "$UNIVERSAL" "$TOOLS"

dotnet publish "$ROOT/src/ScreenCatch.App/ScreenCatch.App.csproj" \
  --configuration "$CONFIGURATION" --runtime osx-x64 --self-contained true \
  --output "$X64" -p:DebugType=None -p:DebugSymbols=false
dotnet publish "$ROOT/src/ScreenCatch.App/ScreenCatch.App.csproj" \
  --configuration "$CONFIGURATION" --runtime osx-arm64 --self-contained true \
  --output "$ARM64" -p:DebugType=None -p:DebugSymbols=false

ditto "$X64" "$UNIVERSAL"
while IFS= read -r -d '' x64_file; do
  relative="${x64_file#"$X64"/}"
  arm64_file="$ARM64/$relative"
  universal_file="$UNIVERSAL/$relative"
  if [[ -f "$arm64_file" ]] && file -b "$x64_file" | grep -q 'Mach-O' && file -b "$arm64_file" | grep -q 'Mach-O'; then
    lipo -create "$x64_file" "$arm64_file" -output "$universal_file"
  fi
done < <(find "$X64" -type f -print0)

mkdir -p "$MACOS"
ditto "$UNIVERSAL" "$MACOS"

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

for tool in ffmpeg ffprobe; do
  if [[ "$tool" == "ffmpeg" ]]; then
    x64_url="https://evermeet.cx/ffmpeg/ffmpeg-8.1.2.zip"
    x64_sha256="e91df72a1ee7c26606f90dd2dd4dcccc6a75140ff9ea6fdd50faae828b82ba69"
    arm64_url="https://github.com/charlienovember/videobot-ffmpeg/releases/download/ffmpeg-n8.1.2/ffmpeg-n8.1.2-macos-arm64.zip"
    arm64_sha256="7c3f9560d1d018746a3fbca4f89d597ad4d45739abc3efc45f6360c9add3e614"
  else
    x64_url="https://evermeet.cx/ffmpeg/ffprobe-8.1.2.zip"
    x64_sha256="399b93f0b9862f69767afa343e90c2f48d7e7958cadbb6deb76a012d0e3b7ce3"
    arm64_url="https://github.com/charlienovember/videobot-ffmpeg/releases/download/ffmpeg-n8.1.2/ffprobe-n8.1.2-macos-arm64.zip"
    arm64_sha256="d86c980604ab0d8c40b3c17c9c3fcb388fb341ea4bd470ab28f9d96c5f3fb812"
  fi
  x64_archive="$WORK/$tool-x64.zip"
  arm64_archive="$WORK/$tool-arm64.zip"
  x64_extract="$WORK/$tool-x64"
  arm64_extract="$WORK/$tool-arm64"
  mkdir -p "$x64_extract" "$arm64_extract"
  download_verified "$x64_url" "$x64_sha256" "$x64_archive"
  download_verified "$arm64_url" "$arm64_sha256" "$arm64_archive"
  ditto -x -k "$x64_archive" "$x64_extract"
  ditto -x -k "$arm64_archive" "$arm64_extract"
  x64_binary="$(find "$x64_extract" -type f -name "$tool" -print -quit)"
  arm64_binary="$(find "$arm64_extract" -type f -name "$tool" -print -quit)"
  if [[ -z "$x64_binary" || -z "$arm64_binary" ]]; then
    echo "Downloaded archives did not contain both $tool architectures" >&2
    exit 1
  fi
  lipo -create "$x64_binary" "$arm64_binary" -output "$TOOLS/$tool"
  lipo -archs "$TOOLS/$tool" | grep -q 'x86_64'
  lipo -archs "$TOOLS/$tool" | grep -q 'arm64'
  chmod 755 "$TOOLS/$tool"
done

mkdir -p "$CONTENTS/Resources"
cat > "$CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>ScreenCatch</string>
  <key>CFBundleExecutable</key><string>ScreenCatch</string>
  <key>CFBundleIdentifier</key><string>com.rwrife.screencatch</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>ScreenCatch</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSMicrophoneUsageDescription</key><string>ScreenCatch uses the microphone only when microphone recording is enabled.</string>
  <key>NSScreenCaptureUsageDescription</key><string>ScreenCatch needs Screen Recording permission to capture the selected display or window.</string>
</dict>
</plist>
PLIST

chmod 755 "$MACOS/ScreenCatch"
plutil -lint "$CONTENTS/Info.plist"
if [[ -n "${MACOS_CODESIGN_IDENTITY:-}" ]]; then
  ENTITLEMENTS="$WORK/entitlements.plist"
  cat > "$ENTITLEMENTS" <<'ENTITLEMENTS_PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>com.apple.security.cs.allow-jit</key><true/>
  <key>com.apple.security.cs.allow-unsigned-executable-memory</key><true/>
</dict>
</plist>
ENTITLEMENTS_PLIST
  plutil -lint "$ENTITLEMENTS"
  codesign --force --deep --options runtime --timestamp --entitlements "$ENTITLEMENTS" \
    --sign "$MACOS_CODESIGN_IDENTITY" "$APP"
else
  echo "MACOS_CODESIGN_IDENTITY is unset; creating an ad-hoc signed development artifact."
  codesign --force --deep --sign - "$APP"
fi
lipo -archs "$MACOS/ScreenCatch" | grep -q 'x86_64'
lipo -archs "$MACOS/ScreenCatch" | grep -q 'arm64'

DMG_ROOT="$WORK/dmg"
mkdir -p "$DMG_ROOT"
ditto "$APP" "$DMG_ROOT/ScreenCatch.app"
ln -s /Applications "$DMG_ROOT/Applications"
DMG="$OUTPUT/screencatch-macOS-universal.dmg"
hdiutil create -volname ScreenCatch -srcfolder "$DMG_ROOT" -ov -format UDZO "$DMG"

if [[ -n "${MACOS_NOTARY_PROFILE:-}" ]]; then
  xcrun notarytool submit "$DMG" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
  xcrun stapler staple "$DMG"
fi

rm -rf "$WORK"
echo "Created $DMG"
