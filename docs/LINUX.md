# Accessible Arena on Linux

MTG Arena has no Linux version, but it runs well under Steam's Proton, Lutris or plain Wine, and MelonLoader runs with it. The one thing that does not carry over is speech: the mod's Windows speech library (Prism) talks to NVDA, JAWS and SAPI, and none of them exist on a Linux desktop. Under Wine the mod therefore speaks through speech-dispatcher, the speech server Orca and most Linux desktops use.

Everything else (keyboard navigation, announcements, settings, updates) is the same code as on Windows.

## Requirements

- MTG Arena installed through Steam (Proton), Lutris or Wine
- speech-dispatcher (package `speech-dispatcher` on Debian, Ubuntu, Fedora and Arch)
- Python 3 (used by the small speech bridge described below)
- Orca is optional. The mod speaks through speech-dispatcher whether Orca runs or not

## Installation

From a copy of this repository, or just its `linux` folder:

```
cd linux
./install.sh
```

The installer:

- finds MTGA in your Steam libraries (native, Flatpak or Snap Steam), in a Lutris prefix, or in `~/.wine`; pass `--game-dir "/path/to/MTGA"` if it does not
- installs MelonLoader, unless it is already there
- builds `AccessibleArena.dll` from the source folder it sits in, against your own game files, and installs it with `prism.dll`. If no .NET SDK (6.0 or newer) is installed, it first downloads a private one into `~/.local/share/accessible-arena/dotnet`, without root. The build log goes to `~/.local/share/accessible-arena/build.log`
- `--dll path/to/AccessibleArena.dll` installs a DLL you built yourself instead; `--release` installs the latest release DLL (only useful once releases include Linux support)
- installs the speech bridge as a systemd user service (skip with `--no-service`)
- prints the launch option MelonLoader needs

Then set the launch option, because MelonLoader loads through `version.dll` and Wine prefers its own copy:

- Steam: right-click MTG Arena, Properties, General, Launch options: `WINEDLLOVERRIDES="version=n,b" %command%`
- Lutris: Configure, Runner options, DLL overrides, add `version` with value `n,b`
- Plain Wine: `WINEDLLOVERRIDES="version=n,b" wine MTGA.exe`

To update, download the new source and run `./install.sh` again. The in-game update (F5) does not replace a build made by the installer, because a release DLL might lack the Linux speech code; it tells you to run the installer instead.

To remove the mod: `./install.sh --uninstall`. MelonLoader stays in place; the private .NET SDK is removed.

## How speech works under Wine

Windows code inside Wine cannot open speech-dispatcher's Unix socket, but its TCP sockets are ordinary Linux sockets, and Wine can start native Linux programs. At startup the mod tries, in order:

1. SSIP (speech-dispatcher's protocol) over TCP on `127.0.0.1:6560`. Something answers there when the bridge service is running, or when speech-dispatcher itself is configured for `inet_socket`.
2. An [nvda2speechd](https://github.com/RastislavKish/nvda2speechd) server on `ws://127.0.0.1:3457` (or `NVDA2SPEECHD_HOST`). If you already run it for other Windows games, the mod uses it as it is; nvda2speechd's client DLL is not needed, the mod speaks its protocol itself.
3. If nothing answers, the mod writes the bridge (`UserData/AccessibleArena-speechd-bridge.py`) and starts it with the system Python, then connects to it. The bridge exits 30 seconds after the game closes.
4. If that fails too, one `spd-say` call per announcement.
5. If none of these works, Prism, as on Windows.

The MelonLoader log (`MelonLoader/Latest.log` in the game folder) says which one was picked, in a line starting with `[Speech] speech-dispatcher ready`.

Steam runs Proton games inside a container (pressure-vessel) that may not see the host's speech-dispatcher socket or programs. The bridge service runs outside the container and only shares a TCP port with the game, which works either way. That is why the installer sets it up as a service.

The bridge listens on 127.0.0.1 only. Any program of any user on the same computer can send it text to speak, the same as with speech-dispatcher's own `inet_socket` mode.

## Settings

"Speech output" in the mod settings (F2) lists Speech Dispatcher first under Wine, and Prism's backends after it once Prism has been loaded. Automatic selection uses Speech Dispatcher whenever it can be reached.

Both normal and urgent announcements go through speech-dispatcher; an urgent one cancels what the mod is saying. The urgent volume setting only applies to Prism's SAPI channel.

The mod's speech-dispatcher connection is separate from Orca's, so Orca's rate and voice do not apply to it; speech-dispatcher's defaults do (set them with `spd-conf`). To set them for the mod only, add environment variables to the launch options, before `%command%`:

- `AA_SPEECHD_RATE`: -100 to 100
- `AA_SPEECHD_PITCH`: -100 to 100
- `AA_SPEECHD_VOLUME`: -100 to 100
- `AA_SPEECHD_LANGUAGE`: language code, for example `pl` or `de`
- `AA_SPEECHD_MODULE`: output module, for example `espeak-ng` or `rhvoice`
- `AA_SPEECHD_VOICE`: synthesis voice name of that module

Example for Steam: `AA_SPEECHD_RATE=40 AA_SPEECHD_LANGUAGE=pl WINEDLLOVERRIDES="version=n,b" %command%`

Other variables:

- `AA_SPEECHD_PORT`: TCP port of the bridge (default 6560). Change it in the service file too
- `SPEECHD_ADDRESS=inet_socket:host:port`: connect to a speech-dispatcher at that address
- `AA_FORCE_LINUX_SPEECH=1` or `0`: override Wine detection

## Troubleshooting

No speech at all:

- Check the bridge service: `systemctl --user status accessible-arena-speech`
- Check speech-dispatcher outside the game: `spd-say "test"`
- Look in `MelonLoader/Latest.log` for lines starting with `[Speech]`
- If the log has no MelonLoader lines at all, the `version=n,b` launch option is missing

Speech is in the wrong language: set `AA_SPEECHD_LANGUAGE` as above, or change the default language with `spd-conf`.

Orca reads things while you play: the game window has no accessibility tree under Wine, so Orca stays mostly quiet. If it speaks over the mod, mute it while playing (Orca's "toggle speech" command).

## Building on Linux

`install.sh` does all of this for you. To build by hand: the mod builds with the .NET SDK on Linux. The project looks for MTGA in the default Steam library paths; for another location create `src/local.props`:

```xml
<Project>
  <PropertyGroup>
    <MtgaPath>/home/me/Games/MTGA</MtgaPath>
  </PropertyGroup>
</Project>
```

Then `dotnet build src/AccessibleArena.csproj` builds and copies the DLL into the game's `Mods` folder.
