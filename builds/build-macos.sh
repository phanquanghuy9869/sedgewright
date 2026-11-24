#!/usr/bin/env bash
# Builds a self-contained Sedgewright Velopack release for macOS.
#
# Usage: bash builds/build-macos.sh [--version 1.2.0] [--rid osx-arm64|osx-x64]
#   --version  Release version in semver format (default: 0.0.1)
#   --rid      Target runtime identifier (default: host architecture)
#
# Uses an installed .NET 10+ SDK or installs .NET 11 for the current user.
# Installs the matching vpk CLI if it is missing.
set -euo pipefail

PACK_ID="Sedgewright"
PACK_TITLE="Sedgewright"
MAIN_EXE="Sedgewright"
VELOPACK_VERSION="1.2.161"
DOTNET_CHANNEL="11.0"
MINIMUM_SDK_MAJOR=11
DEFAULT_VERSION="0.0.1"
SEMVER_PATTERN='^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.+-]+)?$'

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/src/Sedgewright/Sedgewright.csproj"
ICON_PATH="$REPO_ROOT/src/Sedgewright/Assets/app-icon.icns"

step() {
    printf '\033[36m==> %s\033[0m\n' "$1"
}

die() {
    echo "error: $1" >&2
    exit 1
}

usage() {
    sed -n '2,7p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

VERSION="$DEFAULT_VERSION"
RUNTIME_IDENTIFIER=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="${2:?--version requires a value}"; shift 2 ;;
        --rid) RUNTIME_IDENTIFIER="${2:?--rid requires a value}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "unknown argument: $1" ;;
    esac
done

[[ "$VERSION" =~ $SEMVER_PATTERN ]] || die "version '$VERSION' is not a valid semver string"
[[ "$(uname -s)" == "Darwin" ]] || die "build-macos.sh must run on macOS; use build.ps1 on Windows or build-linux on Linux"

if [[ -z "$RUNTIME_IDENTIFIER" ]]; then
    case "$(uname -m)" in
        arm64) RUNTIME_IDENTIFIER="osx-arm64" ;;
        x86_64) RUNTIME_IDENTIFIER="osx-x64" ;;
        *) die "unsupported macOS architecture: $(uname -m)" ;;
    esac
fi
case "$RUNTIME_IDENTIFIER" in
    osx-x64|osx-arm64) ;;
    *) die "unsupported macOS runtime identifier: $RUNTIME_IDENTIFIER" ;;
esac

[[ -f "$PROJECT_PATH" ]] || die "Sedgewright project was not found at $PROJECT_PATH"
[[ -f "$ICON_PATH" ]] || die "macOS app icon was not found at $ICON_PATH"

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RUNTIME_IDENTIFIER"
RELEASE_DIR="$REPO_ROOT/artifacts/releases/$RUNTIME_IDENTIFIER"

sdk_available() {
    command -v dotnet >/dev/null 2>&1 || return 1
    dotnet --list-sdks 2>/dev/null | awk -F. -v min="$MINIMUM_SDK_MAJOR" '$1 + 0 >= min { found = 1 } END { exit !found }'
}

install_dotnet_sdk() {
    local install_dir="$HOME/.dotnet"
    local install_script
    install_script="$(mktemp)"
    trap 'rm -f "$install_script"' EXIT

    step "Installing .NET SDK $DOTNET_CHANNEL to $install_dir"
    curl -sSfL https://dot.net/v1/dotnet-install.sh -o "$install_script"
    bash "$install_script" --channel "$DOTNET_CHANNEL" --install-dir "$install_dir" --no-path
    rm -f "$install_script"
    trap - EXIT

    export DOTNET_ROOT="$install_dir"
    export PATH="$install_dir:$PATH"
}

install_vpk() {
    export PATH="$HOME/.dotnet/tools:$PATH"
    local installed_version
    installed_version="$(dotnet tool list --global | awk '$1 == "vpk" { print $2 }')"

    if [[ "$installed_version" == "$VELOPACK_VERSION" ]]; then
        command -v vpk >/dev/null 2>&1 || die "vpk $VELOPACK_VERSION is not on PATH"
        return
    fi

    if [[ -n "$installed_version" ]]; then
        step "Updating vpk from $installed_version to $VELOPACK_VERSION to match the Velopack package"
        dotnet tool update --global vpk --version "$VELOPACK_VERSION"
    else
        step "Installing vpk $VELOPACK_VERSION"
        dotnet tool install --global vpk --version "$VELOPACK_VERSION"
    fi
    command -v vpk >/dev/null 2>&1 || die "vpk is not on PATH after installation"
}

step "Checking .NET SDK (requires $MINIMUM_SDK_MAJOR or newer)"
if ! sdk_available; then
    install_dotnet_sdk
    sdk_available || die ".NET SDK $MINIMUM_SDK_MAJOR or newer is still unavailable after installation"
fi

install_vpk

rm -rf "$PUBLISH_DIR" "$RELEASE_DIR"

step "Publishing $PACK_ID $VERSION for $RUNTIME_IDENTIFIER"
dotnet publish "$PROJECT_PATH" -c Release -r "$RUNTIME_IDENTIFIER" --self-contained true "-p:Version=$VERSION" -o "$PUBLISH_DIR"

step "Packing Velopack release into $RELEASE_DIR"
vpk pack \
    --packId "$PACK_ID" \
    --packVersion "$VERSION" \
    --packDir "$PUBLISH_DIR" \
    --mainExe "$MAIN_EXE" \
    --packTitle "$PACK_TITLE" \
    --icon "$ICON_PATH" \
    --runtime "$RUNTIME_IDENTIFIER" \
    --outputDir "$RELEASE_DIR"

compgen -G "$RELEASE_DIR/releases.*.json" >/dev/null || die "missing releases.*.json in $RELEASE_DIR"
compgen -G "$RELEASE_DIR/$PACK_ID-$VERSION*-full.nupkg" >/dev/null || die "missing full $PACK_ID $VERSION package in $RELEASE_DIR"
APP_ZIP="$RELEASE_DIR/$PACK_ID-osx-Portable.zip"
[[ -f "$APP_ZIP" ]] || die "missing $PACK_ID-osx-Portable.zip in $RELEASE_DIR"
unzip -Z1 "$APP_ZIP" | grep -Eq "(^|/)${PACK_ID}\\.app/Contents/Info\\.plist$" ||
    die "missing $PACK_ID.app bundle in $APP_ZIP"

step 'Release files:'
ls -1 "$RELEASE_DIR"
