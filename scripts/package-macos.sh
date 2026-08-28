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
has_architecture() {
  local architectures="$1"
  local expected="$2"
  [[ " $architectures " == *" $expected "* ]]
}

while IFS= read -r -d '' x64_file; do
  relative="${x64_file#"$X64"/}"
  arm64_file="$ARM64/$relative"
  universal_file="$UNIVERSAL/$relative"
  if [[ -f "$arm64_file" ]] && file -b "$x64_file" | grep -q 'Mach-O' && file -b "$arm64_file" | grep -q 'Mach-O'; then
    x64_architectures="$(lipo -archs "$x64_file")"
    arm64_architectures="$(lipo -archs "$arm64_file")"

    if has_architecture "$x64_architectures" x86_64 && has_architecture "$x64_architectures" arm64; then
      continue
    fi
    if has_architecture "$arm64_architectures" x86_64 && has_architecture "$arm64_architectures" arm64; then
      cp "$arm64_file" "$universal_file"
      continue
    fi
    if has_architecture "$x64_architectures" x86_64 && has_architecture "$arm64_architectures" arm64; then
      lipo -create "$x64_file" "$arm64_file" -output "$universal_file"
      continue
    fi

    echo "Cannot create a universal binary for $relative: x64=[$x64_architectures], arm64=[$arm64_architectures]" >&2
    exit 1
  fi
done < <(find "$X64" -type f -print0)

mkdir -p "$MACOS"
ditto "$UNIVERSAL" "$MACOS"

"$ROOT/scripts/install-ffmpeg-macos.sh" "$TOOLS" universal

while IFS= read -r -d '' binary; do
  if file -b "$binary" | grep -q 'Mach-O'; then
    architectures="$(lipo -archs "$binary")"
    if ! has_architecture "$architectures" x86_64 || ! has_architecture "$architectures" arm64; then
      echo "Packaged Mach-O binary is not universal: ${binary#"$MACOS"/} [$architectures]" >&2
      exit 1
    fi
  fi
done < <(find "$MACOS" -type f -print0)

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
