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
hdiutil create \
  -volname "Polishly" \
  -srcfolder "$DIST_DIR/$APP_NAME" \
  -ov \
  -format UDZO \
  "$DIST_DIR/$DMG_NAME"

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
