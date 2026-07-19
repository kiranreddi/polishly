#!/usr/bin/env bash
# Build a signed Release .app and a distributable .dmg.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="${MARKETING_VERSION:-1.0.0}"
BUILD_DIR="${BUILD_DIR:-$ROOT/.build-release}"
DIST_DIR="${DIST_DIR:-$ROOT/dist}"
APP_NAME="Polishly.app"
DMG_NAME="Polishly-${VERSION}.dmg"

command -v xcodegen >/dev/null || { echo "xcodegen is required (brew install xcodegen)"; exit 1; }
command -v xcodebuild >/dev/null || { echo "xcodebuild is required"; exit 1; }

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

echo "==> Verifying code signature"
codesign --verify --deep --strict --verbose=2 "$APP_SRC"
codesign -dv --verbose=4 "$APP_SRC" 2>&1 | grep -E 'Authority=|TeamIdentifier=|flags=|Identifier='

echo "==> Staging dist/"
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"
cp -R "$APP_SRC" "$DIST_DIR/$APP_NAME"

# Clear quarantine on the staged copy so local open/test is clean.
xattr -cr "$DIST_DIR/$APP_NAME" || true

echo "==> Creating $DMG_NAME"
rm -f "$DIST_DIR/$DMG_NAME"
hdiutil create \
  -volname "Polishly" \
  -srcfolder "$DIST_DIR/$APP_NAME" \
  -ov \
  -format UDZO \
  "$DIST_DIR/$DMG_NAME"

echo "==> Done"
echo "  App: $DIST_DIR/$APP_NAME"
echo "  DMG: $DIST_DIR/$DMG_NAME"
ls -lh "$DIST_DIR"
