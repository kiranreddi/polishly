#!/usr/bin/env bash
# Build a Developer ID–signed Release .app, package a .dmg, and optionally notarize.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="${MARKETING_VERSION:-1.0.0}"
BUILD_DIR="${BUILD_DIR:-$ROOT/.build-release}"
DIST_DIR="${DIST_DIR:-$ROOT/dist}"
APP_NAME="Polishly.app"
DMG_NAME="Polishly-${VERSION}.dmg"
ENTITLEMENTS="$ROOT/Polishly/Polishly.entitlements"
NOTARY_PROFILE="${NOTARY_PROFILE:-polishly}"
# Set NOTARIZE=0 to skip notarization (local smoke only).
NOTARIZE="${NOTARIZE:-1}"

SIGN_IDENTITY="${SIGN_IDENTITY:-Developer ID Application: Kiran Tathekalva (W26KHF87HS)}"

command -v xcodegen >/dev/null || { echo "xcodegen is required (brew install xcodegen)"; exit 1; }
command -v xcodebuild >/dev/null || { echo "xcodebuild is required"; exit 1; }

if ! security find-identity -v -p codesigning | grep -Fq "$SIGN_IDENTITY"; then
  echo "error: signing identity not found: $SIGN_IDENTITY" >&2
  echo "Available identities:" >&2
  security find-identity -v -p codesigning >&2
  exit 1
fi

echo "==> Generating Xcode project"
xcodegen generate

echo "==> Building Release"
rm -rf "$BUILD_DIR"
xcodebuild \
  -project Polishly.xcodeproj \
  -scheme Polishly \
  -configuration Release \
  -derivedDataPath "$BUILD_DIR" \
  -destination 'platform=macOS' \
  ENABLE_CODE_COVERAGE=NO \
  CLANG_ENABLE_CODE_COVERAGE=NO \
  CLANG_COVERAGE_MAPPING=NO \
  SWIFT_ENABLE_TESTABILITY=NO \
  ENABLE_TESTABILITY=NO \
  GCC_INSTRUMENT_PROGRAM_FLOW_ARCS=NO \
  build

APP_SRC="$BUILD_DIR/Build/Products/Release/$APP_NAME"
test -d "$APP_SRC" || { echo "Release app not found at $APP_SRC"; exit 1; }

echo "==> Staging dist/"
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"
cp -R "$APP_SRC" "$DIST_DIR/$APP_NAME"
# Clear inherited quarantine / Finder attrs before distribution signing.
xattr -cr "$DIST_DIR/$APP_NAME" || true

echo "==> Signing with Developer ID (hardened runtime)"
codesign \
  --force \
  --deep \
  --options runtime \
  --timestamp \
  --entitlements "$ENTITLEMENTS" \
  --sign "$SIGN_IDENTITY" \
  "$DIST_DIR/$APP_NAME"

echo "==> Verifying code signature"
codesign --verify --deep --strict --verbose=2 "$DIST_DIR/$APP_NAME"
codesign -dv --verbose=4 "$DIST_DIR/$APP_NAME" 2>&1 | grep -E 'Authority=|TeamIdentifier=|flags=|Identifier=|Timestamp='
spctl --assess --type execute --verbose=4 "$DIST_DIR/$APP_NAME" 2>&1 || true

echo "==> Creating $DMG_NAME"
rm -f "$DIST_DIR/$DMG_NAME"
STAGING_DIR="$DIST_DIR/dmg-staging"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"
cp -R "$DIST_DIR/$APP_NAME" "$STAGING_DIR/$APP_NAME"
ln -s /Applications "$STAGING_DIR/Applications"

DMG_VOLNAME="Polishly"
TMP_DMG="$DIST_DIR/.polishly-layout-tmp.dmg"
MOUNT_POINT="/Volumes/$DMG_VOLNAME"
rm -f "$TMP_DMG"

# Detach a stale mount from a prior failed run before reusing the volume name.
if [[ -d "$MOUNT_POINT" ]]; then
  hdiutil detach "$MOUNT_POINT" -force >/dev/null 2>&1 || true
fi

hdiutil create \
  -volname "$DMG_VOLNAME" \
  -srcfolder "$STAGING_DIR" \
  -fs HFS+ \
  -format UDRW \
  -size 200m \
  "$TMP_DMG"

hdiutil attach "$TMP_DMG" -noautoopen -mountpoint "$MOUNT_POINT"

echo "==> Laying out drag-to-Applications installer window"
# Requires Finder Automation permission the first time this runs interactively;
# grant it in System Settings > Privacy & Security > Automation if it fails.
osascript <<APPLESCRIPT || echo "warning: Finder layout automation failed — DMG will still work, just without the styled install window. Grant Automation access to your terminal for Finder and re-run." >&2
tell application "Finder"
    tell disk "$DMG_VOLNAME"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set the bounds of container window to {200, 120, 760, 480}
        set viewOptions to the icon view options of container window
        set arrangement of viewOptions to not arranged
        set icon size of viewOptions to 128
        set position of item "$APP_NAME" of container window to {140, 180}
        set position of item "Applications" of container window to {420, 180}
        close
        open
        update without registering applications
        delay 1
    end tell
end tell
APPLESCRIPT

hdiutil detach "$MOUNT_POINT" || hdiutil detach "$MOUNT_POINT" -force

echo "==> Compressing $DMG_NAME"
rm -f "$DIST_DIR/$DMG_NAME"
hdiutil convert "$TMP_DMG" -format UDZO -ov -o "$DIST_DIR/$DMG_NAME"
rm -f "$TMP_DMG"
rm -rf "$STAGING_DIR"

if [[ "$NOTARIZE" == "1" ]]; then
  if ! xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" >/dev/null 2>&1; then
    cat >&2 <<EOF
error: notarytool keychain profile "$NOTARY_PROFILE" not found.

Create an app-specific password at https://appleid.apple.com/account/manage
then run:

  xcrun notarytool store-credentials "$NOTARY_PROFILE" \\
    --apple-id "kiranreddi.t@gmail.com" \\
    --team-id "W26KHF87HS" \\
    --password "xxxx-xxxx-xxxx-xxxx"

Or package without notarizing:

  NOTARIZE=0 ./scripts/package-release.sh
EOF
    exit 1
  fi

  echo "==> Submitting DMG for notarization (profile: $NOTARY_PROFILE)"
  xcrun notarytool submit "$DIST_DIR/$DMG_NAME" \
    --keychain-profile "$NOTARY_PROFILE" \
    --wait

  echo "==> Stapling notarization ticket"
  xcrun stapler staple "$DIST_DIR/$DMG_NAME"
  xcrun stapler validate "$DIST_DIR/$DMG_NAME"

  echo "==> Assessing Gatekeeper"
  spctl --assess --type open --context context:primary-signature --verbose=4 "$DIST_DIR/$DMG_NAME" 2>&1 || true
fi

echo "==> Done"
echo "  App: $DIST_DIR/$APP_NAME"
echo "  DMG: $DIST_DIR/$DMG_NAME"
ls -lh "$DIST_DIR"
