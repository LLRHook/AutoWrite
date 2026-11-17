#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/AutoWrite-build.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
APP="$ROOT/dist/AutoWrite.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$WORK/AutoWrite.iconset"
swiftc -swift-version 5 -O -target arm64-apple-macosx14.0 -module-cache-path "$WORK/modules" "$ROOT"/mac/Sources/*.swift "$ROOT/mac/App/main.swift" -o "$APP/Contents/MacOS/AutoWrite"
for size in 16 32 128 256 512; do
  sips -s format png -z "$size" "$size" "$ROOT/assets/AutoWrite-256.png" --out "$WORK/AutoWrite.iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -s format png -z "$double" "$double" "$ROOT/assets/AutoWrite-256.png" --out "$WORK/AutoWrite.iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$WORK/AutoWrite.iconset" -o "$APP/Contents/Resources/AutoWrite.icns"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>AutoWrite</string>
<key>CFBundleIdentifier</key><string>org.autowrite.AutoWrite</string>
<key>CFBundleName</key><string>AutoWrite</string>
<key>CFBundleDisplayName</key><string>AutoWrite</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleIconFile</key><string>AutoWrite</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
codesign --force --sign - --identifier org.autowrite.AutoWrite "$APP"
codesign --verify --strict "$APP"
printf 'PASS build: dist/AutoWrite.app (arm64, ad hoc signature verified)\n'
