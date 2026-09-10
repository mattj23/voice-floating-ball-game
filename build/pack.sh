#!/usr/bin/env bash
#
# Publishes the game and packages it with Velopack on macOS or Linux.
#
#   osx-arm64  .app bundle, .pkg installer, and portable zip (must run on macOS)
#   linux-x64  AppImage
#
# Output goes to artifacts/publish/<runtime> and artifacts/releases/<runtime>.
#
# Usage:
#   build/pack.sh [--runtime osx-arm64|linux-x64] [--version X.Y.Z]
#
# The runtime defaults to the host OS. The version defaults to <Version> in
# VoiceBallGame.App.csproj.
#
# macOS signing is optional. Without it the bundle is unsigned, and Gatekeeper asks the user to
# approve it on first launch. To sign and notarize, set:
#   VPK_SIGN_APP_IDENTITY      "Developer ID Application: ..."
#   VPK_SIGN_INSTALL_IDENTITY  "Developer ID Installer: ..."
#   VPK_NOTARY_PROFILE         a notarytool keychain profile name

set -euo pipefail

# Keep these values synchronized with build/pack.ps1.
PACK_ID="VoiceBallGame"
PACK_TITLE="Voice Ball Game"
PACK_AUTHORS="Matt Jarvis"
BUNDLE_ID="com.mattjarvis.voiceballgame"
VPK_VERSION="1.2.0"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/src/VoiceBallGame.App/VoiceBallGame.App.csproj"

RUNTIME=""
VERSION=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --runtime) RUNTIME="$2"; shift 2 ;;
        --version) VERSION="$2"; shift 2 ;;
        -h|--help) sed -n '2,22p' "$0"; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done

if [[ -z "$RUNTIME" ]]; then
    case "$(uname -s)" in
        Darwin) RUNTIME="osx-arm64" ;;
        Linux) RUNTIME="linux-x64" ;;
        *) echo "Pass --runtime; on Windows use build/pack.ps1." >&2; exit 2 ;;
    esac
fi

case "$RUNTIME" in
    osx-arm64)
        if [[ "$(uname -s)" != "Darwin" ]]; then
            echo "osx-arm64 can only be packaged on macOS." >&2
            exit 2
        fi
        ;;
    linux-x64) ;;
    *) echo "Unsupported runtime: $RUNTIME" >&2; exit 2 ;;
esac

if ! command -v vpk >/dev/null 2>&1; then
    echo "vpk was not found. Install it with: dotnet tool install -g vpk --version $VPK_VERSION" >&2
    exit 1
fi

if [[ -z "$VERSION" ]]; then
    VERSION="$(dotnet msbuild "$PROJECT" -getProperty:Version | tr -d '[:space:]')"
    if [[ -z "$VERSION" ]]; then
        echo "Could not read <Version> from VoiceBallGame.App.csproj. Pass --version." >&2
        exit 1
    fi
fi

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RUNTIME"
RELEASE_DIR="$REPO_ROOT/artifacts/releases/$RUNTIME"

echo "Packaging $PACK_ID $VERSION for $RUNTIME"

# A stale publish folder can include files from an earlier build in the package.
rm -rf "$PUBLISH_DIR"

set -x
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    "-p:Version=$VERSION" \
    -o "$PUBLISH_DIR"
{ set +x; } 2>/dev/null

COMMON=(
    --packId "$PACK_ID"
    --packVersion "$VERSION"
    --packDir "$PUBLISH_DIR"
    --packTitle "$PACK_TITLE"
    --packAuthors "$PACK_AUTHORS"
    --outputDir "$RELEASE_DIR"
    --runtime "$RUNTIME"
    --mainExe VoiceBallGame.App
)

case "$RUNTIME" in
    osx-arm64)
        # The bundle's Info.plist must declare why the app uses the microphone, or macOS refuses
        # audio input without prompting. The template is filled in with this release's values.
        PLIST="$REPO_ROOT/artifacts/Info.plist"
        mkdir -p "$(dirname "$PLIST")"
        sed -e "s/@BUNDLE_ID@/$BUNDLE_ID/g" \
            -e "s/@VERSION@/$VERSION/g" \
            -e "s/@TITLE@/$PACK_TITLE/g" \
            "$REPO_ROOT/build/macos/Info.plist" > "$PLIST"

        OSX_ARGS=(--bundleId "$BUNDLE_ID" --plist "$PLIST")

        if [[ -n "${VPK_SIGN_APP_IDENTITY:-}" ]]; then
            OSX_ARGS+=(--signAppIdentity "$VPK_SIGN_APP_IDENTITY"
                       --signEntitlements "$REPO_ROOT/build/macos/entitlements.plist")
        fi
        if [[ -n "${VPK_SIGN_INSTALL_IDENTITY:-}" ]]; then
            OSX_ARGS+=(--signInstallIdentity "$VPK_SIGN_INSTALL_IDENTITY")
        fi
        if [[ -n "${VPK_NOTARY_PROFILE:-}" ]]; then
            OSX_ARGS+=(--notaryProfile "$VPK_NOTARY_PROFILE")
        fi

        set -x
        vpk pack "${COMMON[@]}" "${OSX_ARGS[@]}"
        { set +x; } 2>/dev/null
        ;;
    linux-x64)
        set -x
        vpk pack "${COMMON[@]}" --categories "Education;Science"
        { set +x; } 2>/dev/null
        ;;
esac

echo "Done. Packages are in $RELEASE_DIR"
