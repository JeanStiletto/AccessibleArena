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
#   5. puts an "MTG Arena (Accessible Arena)" shortcut on the desktop and in the
#      applications menu; it starts the speech bridge if needed, then the game
#   6. sets the launch option MelonLoader needs under Wine (WINEDLLOVERRIDES="version=n,b")
#      in Steam's settings; Steam must be closed for that, otherwise it prints the steps
#
# Usage: ./install.sh [--game-dir DIR] [--dll FILE] [--release] [--no-service]
#                     [--no-shortcut] [--uninstall]
#   --dll FILE     install this AccessibleArena.dll instead of building
#   --release      install the latest release DLL instead of building (only once releases
#                  include Linux support)
#   --no-shortcut  do not create the desktop and menu shortcut
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
INSTALL_SHORTCUT=1
UNINSTALL=0
STEAM_APP_ID=2141910
LAUNCHER="$BRIDGE_DIR/launch-mtga.sh"
SHORTCUT_NAME="accessible-arena-mtga.desktop"
MENU_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"

say() { printf '%s\n' "$*"; }
die() { printf 'Error: %s\n' "$*" >&2; exit 1; }

usage() {
    sed -n '2,23p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
}

while [ $# -gt 0 ]; do
    case "$1" in
        --game-dir) GAME_DIR="${2:-}"; shift 2 ;;
        --dll) LOCAL_DLL="${2:-}"; shift 2 ;;
        --release) USE_RELEASE=1; shift ;;
        --no-service) INSTALL_SERVICE=0; shift ;;
        --no-shortcut) INSTALL_SHORTCUT=0; shift ;;
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

desktop_dir() {
    local dir=""
    command -v xdg-user-dir >/dev/null 2>&1 && dir="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
    # xdg-user-dir answers $HOME when no desktop folder is configured
    if [ -z "$dir" ] || [ "$dir" = "$HOME" ]; then dir="$HOME/Desktop"; fi
    printf '%s\n' "$dir"
}

# --- Locate the game --------------------------------------------------------

STEAM_ROOTS=(
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
    "$HOME/snap/steam/common/.local/share/Steam"
)

steam_libraries() {
    local root vdf
    for root in "${STEAM_ROOTS[@]}"; do
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
    if [ -f "$MENU_DIR/$SHORTCUT_NAME" ] || [ -f "$(desktop_dir)/$SHORTCUT_NAME" ]; then
        rm -f "$MENU_DIR/$SHORTCUT_NAME" "$(desktop_dir)/$SHORTCUT_NAME"
        say "Removed the desktop and menu shortcut."
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
    if [ "$INSTALL_SHORTCUT" -eq 1 ]; then
        say "For Steam/Proton, start the game from the desktop shortcut: it starts the bridge first."
    else
        say "For Steam/Proton, run this before starting the game: python3 $BRIDGE_DIR/speechd-bridge.py &"
    fi
fi

# --- Desktop shortcut -----------------------------------------------------------

# The launcher starts the speech bridge when nothing answers on its port (no systemd service,
# or the service is stopped), then starts the game the way it was installed.
write_launcher() {
    local launch icon
    case "$GAME_DIR" in
        */steamapps/common/*)
            launch="steam steam://rungameid/$STEAM_APP_ID"
            icon="steam"
            ;;
        */drive_c/*)
            launch="cd $(printf '%q' "$GAME_DIR") && WINEPREFIX=$(printf '%q' "${GAME_DIR%%/drive_c/*}") WINEDLLOVERRIDES=\"version=n,b\" exec wine MTGA.exe"
            icon="applications-games"
            ;;
        *)
            launch="cd $(printf '%q' "$GAME_DIR") && WINEDLLOVERRIDES=\"version=n,b\" exec wine MTGA.exe"
            icon="applications-games"
            ;;
    esac

    cat >"$LAUNCHER" <<LAUNCHER_EOF
#!/usr/bin/env bash
# Written by Accessible Arena's install.sh: starts the speech bridge if needed, then MTG Arena.
if ! (exec 3<>/dev/tcp/127.0.0.1/6560) 2>/dev/null; then
    nohup python3 $(printf '%q' "$BRIDGE_DIR/speechd-bridge.py") --host 127.0.0.1 --port 6560 >/dev/null 2>&1 &
    sleep 1
fi
$launch
LAUNCHER_EOF
    chmod +x "$LAUNCHER"
    SHORTCUT_ICON="$icon"
}

write_shortcut() { # path
    cat >"$1" <<SHORTCUT_EOF
[Desktop Entry]
Type=Application
Name=MTG Arena (Accessible Arena)
Name[pl]=MTG Arena (Accessible Arena)
Comment=Magic: The Gathering Arena with screen reader support
Comment[pl]=Magic: The Gathering Arena z obsługą czytnika ekranu
Exec="$LAUNCHER"
Icon=$SHORTCUT_ICON
Terminal=false
Categories=Game;CardGame;
SHORTCUT_EOF
    chmod +x "$1"
    # GNOME, MATE and Cinnamon only run desktop files that are marked trusted
    command -v gio >/dev/null 2>&1 && gio set "$1" metadata::trusted true >/dev/null 2>&1 || true
}

if [ "$INSTALL_SHORTCUT" -eq 1 ]; then
    write_launcher
    mkdir -p "$MENU_DIR"
    write_shortcut "$MENU_DIR/$SHORTCUT_NAME"
    DESKTOP="$(desktop_dir)"
    if mkdir -p "$DESKTOP" 2>/dev/null; then
        write_shortcut "$DESKTOP/$SHORTCUT_NAME"
        say "Shortcut \"MTG Arena (Accessible Arena)\" created on the desktop and in the applications menu."
    else
        say "Shortcut \"MTG Arena (Accessible Arena)\" created in the applications menu."
    fi
fi

# --- Launch option ---------------------------------------------------------------

# MelonLoader loads through version.dll, and Wine prefers its own copy unless told otherwise.
# Steam keeps each game's launch options in userdata/<account>/config/localconfig.vdf.
# Prints "set", "already" or "missing" (MTG Arena has no entry in that file).
set_steam_launch_option() { # localconfig.vdf
    python3 - "$1" "$STEAM_APP_ID" <<'PY'
import re, shutil, sys

path, app_id = sys.argv[1], sys.argv[2]
OVERRIDE = 'version=n,b'
text = open(path, encoding='utf-8').read()

# Tokens of Valve's text KeyValues format: quoted strings and braces.
tokens = [(m.start(), m.end(), m.group(1), m.group(2))
          for m in re.finditer(r'"((?:[^"\\]|\\.)*)"|([{}])|//[^\n]*', text) if not m.group(0).startswith('//')]

want = ['userlocalconfigstore', 'software', 'valve', 'steam', 'apps', app_id]
stack, pending_key = [], None
apps_close = app_open = app_close = None
option = None  # (start, end, value) of LaunchOptions inside the app block
i = 0
while i < len(tokens):
    start, end, string, brace = tokens[i]
    if brace == '{':
        stack.append(pending_key)
        if [k.lower() for k in stack] == want:
            app_open = end
    elif brace == '}':
        keys = [k.lower() for k in stack]
        if keys == want:
            app_close = start
        elif keys == want[:-1]:
            apps_close = start
        stack.pop()
    else:
        nxt = tokens[i + 1] if i + 1 < len(tokens) else None
        if nxt and nxt[3] is None:  # key followed by a value
            if [k.lower() for k in stack] == want and string.lower() == 'launchoptions':
                option = (nxt[0], nxt[1], nxt[2])
            i += 2
            continue
        pending_key = string
    i += 1

def indent_before(pos):
    line_start = text.rfind('\n', 0, pos) + 1
    return text[line_start:pos]

def vdf(value):
    return '"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"'

if option:
    old = option[2].replace('\\"', '"').replace('\\\\', '\\')
    if OVERRIDE in old:
        print('already'); sys.exit(0)
    if 'WINEDLLOVERRIDES="' in old:
        new = old.replace('WINEDLLOVERRIDES="', 'WINEDLLOVERRIDES="' + OVERRIDE + ';', 1)
    elif 'WINEDLLOVERRIDES=' in old:
        new = old.replace('WINEDLLOVERRIDES=', 'WINEDLLOVERRIDES=' + OVERRIDE + ';', 1)
    elif '%command%' in old:
        new = 'WINEDLLOVERRIDES="' + OVERRIDE + '" ' + old
    else:  # bare game arguments
        new = ('WINEDLLOVERRIDES="' + OVERRIDE + '" %command% ' + old).rstrip()
    result = text[:option[0]] + vdf(new) + text[option[1]:]
elif app_close is not None:
    pad = indent_before(app_close) + '\t'
    entry = pad + '"LaunchOptions"\t\t' + vdf('WINEDLLOVERRIDES="' + OVERRIDE + '" %command%') + '\n'
    line_start = text.rfind('\n', 0, app_close) + 1
    result = text[:line_start] + entry + text[line_start:]
elif apps_close is not None:
    pad = indent_before(apps_close) + '\t'
    block = (pad + '"' + app_id + '"\n' + pad + '{\n' + pad + '\t"LaunchOptions"\t\t'
             + vdf('WINEDLLOVERRIDES="' + OVERRIDE + '" %command%') + '\n' + pad + '}\n')
    line_start = text.rfind('\n', 0, apps_close) + 1
    result = text[:line_start] + block + text[line_start:]
else:
    print('missing'); sys.exit(0)

shutil.copy2(path, path + '.accessible-arena-backup')
with open(path, 'w', encoding='utf-8') as f:
    f.write(result)
print('set')
PY
}

steam_is_running() {
    pgrep -x steam >/dev/null 2>&1 || pgrep -x steamwebhelper >/dev/null 2>&1
}

say ""
LAUNCH_OPTION_DONE=0
case "$GAME_DIR" in
    */steamapps/common/*)
        configs=()
        while IFS= read -r root; do
            for config in "$root"/userdata/*/config/localconfig.vdf; do
                [ -f "$config" ] && configs+=("$config")
            done
        done < <(for root in "${STEAM_ROOTS[@]}"; do [ -d "$root" ] && readlink -f "$root"; done | awk '!seen[$0]++')

        if [ "${#configs[@]}" -eq 0 ]; then
            say "Steam settings not found, so the launch option could not be set automatically."
        elif steam_is_running; then
            say "Steam is running, so the launch option could not be set: Steam would overwrite it when it closes."
            say "Close Steam completely (Steam menu, Exit) and run install.sh again, or set it by hand as below."
        else
            for config in "${configs[@]}"; do
                case "$(set_steam_launch_option "$config" 2>/dev/null || echo failed)" in
                    set) LAUNCH_OPTION_DONE=1; say "Steam launch option for MTG Arena set (WINEDLLOVERRIDES=\"version=n,b\"). Backup: $config.accessible-arena-backup" ;;
                    already) LAUNCH_OPTION_DONE=1; say "Steam launch option was already set." ;;
                    missing) say "MTG Arena not found in $config (start it once from Steam, then run install.sh again)." ;;
                    *) say "Could not change $config" ;;
                esac
            done
        fi
        ;;
    *)
        # The desktop shortcut starts the game with the override already.
        [ "$INSTALL_SHORTCUT" -eq 1 ] && LAUNCH_OPTION_DONE=1
        ;;
esac

if [ "$LAUNCH_OPTION_DONE" -eq 1 ]; then
    case "$GAME_DIR" in
        */steamapps/common/*) say "Installation finished. Start the game from the desktop shortcut or from Steam." ;;
        *) say "Installation finished. Start the game from the desktop shortcut." ;;
    esac
    say "You should hear: Accessible Arena launched."
else
    say "Installation finished. One step is left, because MelonLoader loads through version.dll:"
    say "Steam: right-click MTG Arena, Properties, General, Launch options, and enter:"
    say "  WINEDLLOVERRIDES=\"version=n,b\" %command%"
    say "Lutris: Configure, Runner options, DLL overrides, add: version = n,b"
    say "Plain Wine: start the game with WINEDLLOVERRIDES=\"version=n,b\" wine MTGA.exe"
    say "Then start the game. You should hear: Accessible Arena launched."
fi
