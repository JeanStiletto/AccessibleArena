#!/usr/bin/env bash
# Accessible Arena installer for Linux (MTG Arena under Steam/Proton, Lutris or plain Wine).
#
# What it does:
#   1. finds the MTGA folder (or takes it from --game-dir)
#   2. installs MelonLoader into it, unless already there
#   3. builds the mod from this source folder against your own game files (it downloads a
#      private .NET SDK into ~/.local/share/accessible-arena if dotnet is not installed)
#      and installs AccessibleArena.dll and prism.dll
#   4. installs the speech bridge as a systemd user service, so the mod can reach
#      speech-dispatcher (Orca's speech server) from inside Wine/Proton
#   5. prints the one launch option MelonLoader needs under Wine
#
# Usage: ./install.sh [--game-dir DIR] [--dll FILE] [--release] [--no-service] [--uninstall]
#   --dll FILE   install this AccessibleArena.dll instead of building
#   --release    install the latest release DLL instead of building (only once releases
#                include Linux support)
#
# Text output only, one line per step, so it reads well with a screen reader.

set -euo pipefail

REPO="JeanStiletto/AccessibleArena"
REPO_RAW="https://raw.githubusercontent.com/${REPO}/main"
RELEASE_URL="https://github.com/${REPO}/releases/latest/download"
SOURCE_REPO="${AA_SOURCE_REPO:-$REPO}"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
MELON_URL="https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip"
BRIDGE_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/accessible-arena"
SERVICE_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"
SERVICE_NAME="accessible-arena-speech.service"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

GAME_DIR=""
LOCAL_DLL=""
USE_RELEASE=0
INSTALL_SERVICE=1
UNINSTALL=0

say() { printf '%s\n' "$*"; }
die() { printf 'Error: %s\n' "$*" >&2; exit 1; }

usage() {
    sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
}

while [ $# -gt 0 ]; do
    case "$1" in
        --game-dir) GAME_DIR="${2:-}"; shift 2 ;;
        --dll) LOCAL_DLL="${2:-}"; shift 2 ;;
        --release) USE_RELEASE=1; shift ;;
        --no-service) INSTALL_SERVICE=0; shift ;;
        --uninstall) UNINSTALL=1; shift ;;
        -h|--help) usage ;;
        *) die "unknown option: $1 (see --help)" ;;
    esac
done

need() { command -v "$1" >/dev/null 2>&1 || die "$1 is required but not installed"; }

download() { # url dest
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL --retry 3 -o "$2" "$1"
    elif command -v wget >/dev/null 2>&1; then
        wget -q -O "$2" "$1"
    else
        die "curl or wget is required"
    fi
}

# --- Locate the game --------------------------------------------------------

steam_libraries() {
    local roots=(
        "$HOME/.local/share/Steam"
        "$HOME/.steam/steam"
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
        "$HOME/snap/steam/common/.local/share/Steam"
    )
    local root vdf
    for root in "${roots[@]}"; do
        [ -d "$root/steamapps" ] && printf '%s\n' "$root"
        vdf="$root/steamapps/libraryfolders.vdf"
        if [ -f "$vdf" ]; then
            sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)".*/\1/p' "$vdf"
        fi
    done
}

find_game() {
    local lib candidate
    while IFS= read -r lib; do
        candidate="$lib/steamapps/common/MTGA"
        if [ -f "$candidate/MTGA.exe" ]; then printf '%s\n' "$candidate"; return 0; fi
    done < <(steam_libraries | awk '!seen[$0]++')

    local prefixes=(
        "${WINEPREFIX:-}"
        "$HOME/Games/magic-the-gathering-arena"
        "$HOME/Games/mtg-arena"
        "$HOME/.wine"
    )
    local prefix
    for prefix in "${prefixes[@]}"; do
        [ -n "$prefix" ] || continue
        for candidate in \
            "$prefix/drive_c/Program Files/Wizards of the Coast/MTGA" \
            "$prefix/drive_c/Program Files (x86)/Wizards of the Coast/MTGA"; do
            if [ -f "$candidate/MTGA.exe" ]; then printf '%s\n' "$candidate"; return 0; fi
        done
    done
    return 1
}

if [ -z "$GAME_DIR" ]; then
    GAME_DIR="$(find_game)" || die "MTG Arena not found. Pass its folder with --game-dir (the folder that contains MTGA.exe)."
fi
[ -f "$GAME_DIR/MTGA.exe" ] || die "MTGA.exe not found in $GAME_DIR"
say "MTG Arena folder: $GAME_DIR"

# --- Uninstall ---------------------------------------------------------------

if [ "$UNINSTALL" -eq 1 ]; then
    rm -f "$GAME_DIR/Mods/AccessibleArena.dll" "$GAME_DIR/prism.dll" \
          "$GAME_DIR/UserData/AccessibleArena-speechd-bridge.py"
    say "Removed the mod from the game folder. MelonLoader was left in place."
    if command -v systemctl >/dev/null 2>&1 && [ -f "$SERVICE_DIR/$SERVICE_NAME" ]; then
        systemctl --user disable --now "$SERVICE_NAME" >/dev/null 2>&1 || true
        rm -f "$SERVICE_DIR/$SERVICE_NAME"
        systemctl --user daemon-reload >/dev/null 2>&1 || true
        say "Removed the speech bridge service."
    fi
    rm -rf "$BRIDGE_DIR"
    say "Done."
    exit 0
fi

need python3
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# --- MelonLoader -------------------------------------------------------------

if [ -f "$GAME_DIR/version.dll" ] && [ -d "$GAME_DIR/MelonLoader" ]; then
    say "MelonLoader is already installed."
else
    say "Downloading MelonLoader..."
    download "$MELON_URL" "$TMP/MelonLoader.zip" || die "could not download MelonLoader from $MELON_URL"
    python3 -m zipfile -e "$TMP/MelonLoader.zip" "$GAME_DIR/"
    say "MelonLoader installed."
fi
mkdir -p "$GAME_DIR/Mods" "$GAME_DIR/UserData"

# --- The mod ------------------------------------------------------------------

# A .NET SDK able to build the mod: the system one if it is 6.0 or newer, otherwise a private
# copy in $BRIDGE_DIR/dotnet (no root needed, removed again by --uninstall).
find_dotnet() {
    local candidate major
    for candidate in "$(command -v dotnet 2>/dev/null || true)" "$BRIDGE_DIR/dotnet/dotnet"; do
        [ -n "$candidate" ] && [ -x "$candidate" ] || continue
        major="$("$candidate" --list-sdks 2>/dev/null | sed -n 's/^\([0-9]*\)\..*/\1/p' | sort -n | tail -1)"
        if [ -n "$major" ] && [ "$major" -ge 6 ]; then
            printf '%s\n' "$candidate"
            return 0
        fi
    done
    return 1
}

# The mod's source: the checkout this script lives in, or a download of SOURCE_REPO.
find_source() {
    if [ -f "$SCRIPT_DIR/../src/AccessibleArena.csproj" ]; then
        (cd "$SCRIPT_DIR/.." && pwd)
        return 0
    fi
    say "Downloading the mod's source from github.com/$SOURCE_REPO..." >&2
    download "https://github.com/$SOURCE_REPO/archive/refs/heads/main.tar.gz" "$TMP/source.tar.gz" \
        || die "could not download the source"
    mkdir -p "$TMP/source"
    tar -xzf "$TMP/source.tar.gz" -C "$TMP/source" --strip-components=1
    printf '%s\n' "$TMP/source"
}

build_mod() {
    local source dotnet out log
    source="$(find_source)"
    [ -f "$source/src/Core/Speech/SpeechDispatcherOutput.cs" ] \
        || die "the source in $source has no Linux speech support; use the repository that contains linux/install.sh"

    if ! dotnet="$(find_dotnet)"; then
        say "Downloading a private .NET SDK (about 200 MB, one time only)..."
        download "$DOTNET_INSTALL_URL" "$TMP/dotnet-install.sh" || die "could not download the .NET installer"
        bash "$TMP/dotnet-install.sh" --channel 8.0 --install-dir "$BRIDGE_DIR/dotnet" >"$TMP/dotnet-install.log" 2>&1 \
            || { cat "$TMP/dotnet-install.log" >&2; die "could not install the .NET SDK"; }
        dotnet="$BRIDGE_DIR/dotnet/dotnet"
    fi

    say "Building Accessible Arena against your copy of the game (takes a minute)..."
    out="$TMP/build"
    log="$BRIDGE_DIR/build.log"
    mkdir -p "$BRIDGE_DIR"
    if ! DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
        "$dotnet" build "$source/src/AccessibleArena.csproj" -c Release -nologo \
            -p:MtgaPath="$GAME_DIR" -p:DeployToMtga=false -p:AaLinuxLocalBuild=true \
            -o "$out" >"$log" 2>&1; then
        grep -E "error" "$log" | head -5 >&2 || true
        die "the build failed; the full log is in $log"
    fi
    cp "$out/AccessibleArena.dll" "$GAME_DIR/Mods/AccessibleArena.dll"
    say "Built and installed AccessibleArena.dll."
}

if [ -n "$LOCAL_DLL" ]; then
    [ -f "$LOCAL_DLL" ] || die "file not found: $LOCAL_DLL"
    cp "$LOCAL_DLL" "$GAME_DIR/Mods/AccessibleArena.dll"
    say "Installed AccessibleArena.dll from $LOCAL_DLL"
elif [ "$USE_RELEASE" -eq 1 ]; then
    say "Downloading Accessible Arena..."
    download "$RELEASE_URL/AccessibleArena.dll" "$TMP/AccessibleArena.dll"
    cp "$TMP/AccessibleArena.dll" "$GAME_DIR/Mods/AccessibleArena.dll"
    say "Installed the latest release AccessibleArena.dll."
else
    build_mod
fi

# prism.dll is the Windows speech library. Under Wine it is only a fallback, but the mod
# expects it in the game folder.
if [ -f "$SCRIPT_DIR/../third_party/prism/x64/prism.dll" ]; then
    cp "$SCRIPT_DIR/../third_party/prism/x64/prism.dll" "$GAME_DIR/prism.dll"
elif download "$RELEASE_URL/prism.dll" "$TMP/prism.dll" 2>/dev/null; then
    cp "$TMP/prism.dll" "$GAME_DIR/prism.dll"
else
    say "Note: prism.dll could not be downloaded; it is only needed as a fallback on Linux."
fi

# --- Speech bridge -------------------------------------------------------------

mkdir -p "$BRIDGE_DIR"
if [ -f "$SCRIPT_DIR/speechd-bridge.py" ]; then
    cp "$SCRIPT_DIR/speechd-bridge.py" "$BRIDGE_DIR/speechd-bridge.py"
else
    download "$REPO_RAW/linux/speechd-bridge.py" "$BRIDGE_DIR/speechd-bridge.py"
fi
chmod +x "$BRIDGE_DIR/speechd-bridge.py"

if ! command -v speech-dispatcher >/dev/null 2>&1 && ! command -v spd-say >/dev/null 2>&1; then
    say "Warning: speech-dispatcher is not installed. Install it with your package manager (package name: speech-dispatcher)."
fi

if [ "$INSTALL_SERVICE" -eq 1 ] && command -v systemctl >/dev/null 2>&1 && systemctl --user show-environment >/dev/null 2>&1; then
    mkdir -p "$SERVICE_DIR"
    if [ -f "$SCRIPT_DIR/$SERVICE_NAME" ]; then
        cp "$SCRIPT_DIR/$SERVICE_NAME" "$SERVICE_DIR/$SERVICE_NAME"
    else
        download "$REPO_RAW/linux/$SERVICE_NAME" "$SERVICE_DIR/$SERVICE_NAME"
    fi
    systemctl --user daemon-reload
    systemctl --user enable --now "$SERVICE_NAME" >/dev/null
    say "Speech bridge service installed and started ($SERVICE_NAME)."
else
    say "Speech bridge service not installed. The mod starts the bridge itself, which works with Lutris and plain Wine."
    say "For Steam/Proton, run this before starting the game: python3 $BRIDGE_DIR/speechd-bridge.py &"
fi

# --- Launch option ---------------------------------------------------------------

say ""
say "Installation finished. One step is left, because MelonLoader loads through version.dll:"
say "Steam: right-click MTG Arena, Properties, General, Launch options, and enter:"
say "  WINEDLLOVERRIDES=\"version=n,b\" %command%"
say "Lutris: Configure, Runner options, DLL overrides, add: version = n,b"
say "Plain Wine: start the game with WINEDLLOVERRIDES=\"version=n,b\" wine MTGA.exe"
say "Then start the game. You should hear: Accessible Arena launched."
