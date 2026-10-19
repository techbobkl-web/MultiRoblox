#!/bin/bash
# Builds MultiRoblox.app (universal: Apple silicon + Intel), ad-hoc signs it, and zips it.
# Output: mac/dist/MultiRoblox.app and mac/dist/MultiRoblox-mac.zip
set -euo pipefail

cd "$(dirname "$0")/.."
VERSION="${VERSION:-1.0.0}"
DIST=dist
APP="$DIST/MultiRoblox.app"

rm -rf "$DIST"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

swift build -c release --arch arm64 --arch x86_64
cp "$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)/MultiRoblox" "$APP/Contents/MacOS/MultiRoblox"

# App icon
ICONSET="$DIST/AppIcon.iconset"
mkdir -p "$ICONSET"
swift scripts/make-icon.swift "$DIST/icon-1024.png"
for s in 16 32 128 256 512; do
  sips -z $s $s "$DIST/icon-1024.png" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  sips -z $((s * 2)) $((s * 2)) "$DIST/icon-1024.png" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf "$ICONSET" "$DIST/icon-1024.png"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>MultiRoblox</string>
  <key>CFBundleDisplayName</key><string>MultiRoblox</string>
  <key>CFBundleIdentifier</key><string>io.github.techbobkl-web.MultiRoblox</string>
  <key>CFBundleExecutable</key><string>MultiRoblox</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key><string>14.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

# Ad-hoc signature: required to run on Apple silicon. Not notarized (see README for Gatekeeper).
codesign --force --sign - --timestamp=none "$APP"
codesign --verify --strict "$APP"

ditto -c -k --keepParent "$APP" "$DIST/MultiRoblox-mac.zip"
echo "Built $APP and $DIST/MultiRoblox-mac.zip"
