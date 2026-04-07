#!/usr/bin/env bash
# Creates a temporary AeroVim.app bundle from a Debug build for local testing.
# Usage: ./scripts/create-dev-bundle.sh [osx-arm64|osx-x64]
set -euo pipefail

RID="${1:-osx-arm64}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/AeroVim/AeroVim.csproj"
PACKAGING_DIR="$REPO_ROOT/Packaging/macOS"

echo "Building Debug for $RID..."
dotnet build "$PROJECT" --configuration Debug --runtime "$RID" --no-restore --nologo

BUILD_DIR="$REPO_ROOT/AeroVim/bin/Debug/net10.0/$RID"
BUNDLE="$BUILD_DIR/AeroVim.app"

rm -rf "$BUNDLE"
mkdir -p "$BUNDLE/Contents/MacOS"
mkdir -p "$BUNDLE/Contents/Resources"

cp "$PACKAGING_DIR/Info.plist" "$BUNDLE/Contents/"
cp -a "$BUILD_DIR"/* "$BUNDLE/Contents/MacOS/"
rm -rf "$BUNDLE/Contents/MacOS/AeroVim.app"
cp "$PACKAGING_DIR/aerovim.icns" "$BUNDLE/Contents/Resources/"
chmod +x "$BUNDLE/Contents/MacOS/aerovim"

echo "Bundle created at $BUNDLE"
echo "Launching..."
open "$BUNDLE"
