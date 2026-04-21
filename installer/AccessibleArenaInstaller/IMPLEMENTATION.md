# Accessible Arena Installer - Implementation Documentation

## Overview

Single-file C# WinForms installer that:
- Uses standard Windows dialogs (screen reader accessible)
- Installs MelonLoader + mod + dependencies
- Supports updates and uninstallation
- Optional logging for troubleshooting

## Technology Stack

- **Language:** C# (.NET Framework 4.7.2)
- **UI Framework:** WinForms (native Windows dialogs - inherently accessible)
- **File Embedding:** Standard EmbeddedResource (for Tolk DLLs and locale files)
- **Localization:** InstallerLocale static class with embedded JSON resources, 12 languages
- **Admin rights:** Application manifest requesting `requireAdministrator`
- **HTTP client:** System.Net.Http for GitHub API calls
- **ZIP extraction:** System.IO.Compression for MelonLoader installation

## File Delivery Strategy

**Embedded in installer:**
- Tolk.dll (screen reader communication)
- nvdaControllerClient64.dll (NVDA controller)

**Downloaded at install time:**
- MelonLoader (ZIP from official GitHub releases - extracted manually)
- AccessibleArena.dll (from your GitHub releases)

**Rationale:** Tolk DLLs rarely change, MelonLoader updates frequently, mod updates with releases.

## Project Structure

```
installer/
├── release.ps1                              # Local release script (builds, tags, publishes)
└── AccessibleArenaInstaller/
├── AccessibleArenaInstaller.csproj   # Project file
├── app.manifest                         # UAC admin elevation request
├── Program.cs                           # Entry point, CLI args, update check, uninstall logic
├── WelcomeForm.cs                       # Two-page welcome wizard (language + MTGA download)
├── UpdateAvailableForm.cs               # Update available dialog (update/full install/close)
├── MainForm.cs                          # Main installer/updater UI
├── UninstallForm.cs                     # Uninstall UI
├── InstallationManager.cs               # Core file operations
├── MelonLoaderInstaller.cs              # MelonLoader download/extraction
├── GitHubClient.cs                      # GitHub API for downloads
├── RegistryManager.cs                   # Add/Remove Programs registry
├── InstallerLocale.cs                   # Localization system (loads embedded JSON)
├── LanguageDetector.cs                  # OS language detection
├── Config.cs                            # Configuration constants
├── Logger.cs                            # Optional installation logging
├── Locales/                             # Embedded locale JSON files
│   ├── en.json                          # English (source of truth)
│   ├── de.json, fr.json, es.json        # German, French, Spanish
│   ├── it.json, pt-BR.json, ru.json     # Italian, Brazilian Portuguese, Russian
│   ├── pl.json, ja.json, ko.json        # Polish, Japanese, Korean
│   └── zh-CN.json, zh-TW.json          # Simplified/Traditional Chinese
└── Resources/
    ├── Tolk.dll                         # Embedded
    └── nvdaControllerClient64.dll       # Embedded
```

## Installation Flow

### Step 1: Pre-flight Checks (in Program.cs)
1. Check if running as admin (via manifest, should always be true)
2. Check if MTGA.exe is running → Block with message

### Step 2: Version Check
1. Fetch latest mod version from GitHub API (used for display and comparison)
2. Check if mod DLL exists in default MTGA location
3. If mod exists: Get installed version from **registry first** (stores GitHub tag from last install), falling back to DLL assembly version
4. Compare installed vs latest to determine update status

Three outcomes:
- **Update available:** Show Update Available Dialog (Update Mod / Full Install / Close)
- **Mod up to date:** Show "Mod Up to Date" dialog with version, offer Close or Full Reinstall
- **No mod installed:** Proceed directly to Welcome Wizard

### Step 3: Welcome Wizard (two pages)
**Page 1 - Welcome:**
- Mod description and version to be installed (e.g. "Version to install: v0.6")
- Language dropdown (auto-detected from OS, changes installer UI live)
- Next button

**Page 2 - MTGA Download:**
- Instructions to download MTGA if not yet installed
- **Direct Download** button - Downloads MTGAInstaller.exe
- **Download Page** button - Opens MTGA website
- **Back** button - Return to page 1
- **Install Mod** button - Proceeds to installation

### Step 4: Path Detection
- Check registry for previous install location
- Check default: `C:\Program Files\Wizards of the Coast\MTGA`
- Check x86 Program Files as fallback
- If not found: User selects via FolderBrowserDialog

### Step 5: Main Installation (MainForm.cs)
**Full Install Mode:**
1. **Copy Tolk DLLs** - Extract embedded resources to MTGA root
2. **MelonLoader Check/Install:**
   - If not installed: Ask user, then download ZIP and extract
   - If already installed: Ask if user wants to reinstall or keep existing
3. **Create Mods folder** if it doesn't exist
4. **Download Mod DLL** from GitHub releases (no redundant version check)
5. **Configure mod language** if selected
6. **Hide MelonLoader console** - sets `hide_console = true` in `UserData/Loader.cfg`
7. **Copy persistent uninstaller** - `InstallationManager.CopyUninstaller()` copies the running EXE to `<MtgaPath>\AccessibleArena_Uninstaller.exe` so Add/Remove Programs keeps working after the original download is deleted
8. **Register in Add/Remove Programs** - stores GitHub release tag as version; `UninstallString` points at the persistent copy from step 7

**Update Only Mode:**
1. Skip Tolk DLLs and MelonLoader
2. **Fetch latest version** from GitHub (for registry)
3. **Download Mod DLL** from GitHub releases
4. **Update registry** with GitHub release tag

**Version registration:** The registry always stores the GitHub release tag (e.g. "0.6"), not the DLL assembly version. This prevents stale assembly versions from causing perpetual "update available" cycles.

### Step 6: Completion
- Show success message (with first-launch warning if MelonLoader was installed)
- Ask about log file only if there were errors/warnings
- Optionally launch MTGA

## MelonLoader Installation Details

**Important Discovery:** MelonLoader's official installer does NOT support silent/CLI installation. It only has GUI mode.

**Solution:** Manual ZIP extraction (same as their documented "manual install" method):
1. Download `MelonLoader.x64.zip` from GitHub releases
2. Extract to MTGA folder:
   - `version.dll` → MTGA root (proxy DLL that bootstraps MelonLoader)
   - `dobby.dll` → MTGA root (required for Il2Cpp games like MTGA)
   - `MelonLoader/` folder → MTGA root (runtime files)

**First Launch:** After MelonLoader installation, first game launch takes 1-2 minutes while MelonLoader generates managed assemblies from Il2Cpp. This is normal and only happens once.

## Uninstallation

### Persistent Uninstaller in the MTGA Folder

At install time (`InstallationManager.CopyUninstaller()`) the running installer EXE is copied to `<MtgaPath>\AccessibleArena_Uninstaller.exe` (see `Config.UninstallerExeName`). `RegistryManager.Register(..., uninstallerPath)` then writes that stable path into the `UninstallString` / `QuietUninstallString` registry values.

**Why:** Earlier versions registered `Assembly.GetExecutingAssembly().Location` as the uninstall path, which meant the `UninstallString` pointed at the user's `Downloads` folder. If the user deleted the installer (or Windows Storage Sense cleaned it up), Add/Remove Programs would silently fail or 404 on uninstall, leaving MelonLoader and mod files permanently on disk. The persistent copy guarantees uninstall keeps working regardless of what happens to the original download.

**Register signature:** `Register(installPath, version, uninstallerPath = null)`. If the caller passes `null` or a path that doesn't exist on disk, it falls back to the current EXE's location (legacy behaviour — still works if a fresh EXE invocation happens to be in a stable location, but fragile).

### Trigger Methods

- Windows Settings → Apps → Accessible Arena → Uninstall (uses registered `UninstallString`)
- Control Panel → Programs and Features
- Command line (any EXE copy): `AccessibleArenaInstaller.exe /uninstall`
- Silent: `AccessibleArenaInstaller.exe /uninstall /quiet`

Running `AccessibleArena_Uninstaller.exe` directly **without `/uninstall`** enters the normal install wizard (it's the same binary as the installer). If users report being offered a reinstall instead of uninstall, they double-clicked the EXE instead of going through Add/Remove Programs. Not a bug — by design, since the same EXE is both installer and uninstaller.

### What Gets Removed (Always, regardless of MelonLoader checkbox)

`Program.PerformUninstall`:

- `Mods/AccessibleArena.dll` + `.backup`
- `Tolk.dll`, `nvdaControllerClient64.dll` + `.backup` files from MTGA root
- Empty `Mods/` folder (kept if it still contains other files)
- `UserData/AccessibleArena.json` (mod settings)
- `UserData/AccessibleArena/` folder (per-mod `MelonPreferences` data)
- Registry entry under `HKLM\...\Uninstall\AccessibleArena`
- Schedules self-delete of `AccessibleArena_Uninstaller.exe` (see below)

### What Gets Removed Additionally When MelonLoader Checkbox Is Ticked

`Program.UninstallMelonLoader`:

- `MelonLoader/` folder (entire MelonLoader runtime)
- `MelonLoader.backup/` folder (if present from a previous reinstall)
- `version.dll` + `version.dll.backup` from MTGA root
- `dobby.dll` + `dobby.dll.backup` from MTGA root (MTGA is Mono, not Il2Cpp, so these are usually absent — removed defensively)
- `UserData/Loader.cfg`, `UserData/MelonPreferences.cfg`
- Empty `Plugins/`, `UserLibs/`, `UserData/` folders (kept if they still contain files, so other mods are safe)

The checkbox is opt-in and defaults to unchecked, with label "Also remove MelonLoader (only if you don't use other mods)". Keep this UX in mind: if a user only ticks the mod uninstall and not MelonLoader, the game still launches with MelonLoader injecting into the process — which has caused real downstream problems (e.g. PayPal's anti-fraud fingerprinting flagging the account because `version.dll` + `MelonLoader/` are still present in the game folder).

### Self-Deleting Uninstaller

Since the running process is the same EXE as `AccessibleArena_Uninstaller.exe`, we can't delete ourselves directly — Windows holds an exclusive lock on a running executable's image. `Program.ScheduleUninstallerSelfDelete` spawns a detached `cmd.exe` child with:

```
cmd /c ping 127.0.0.1 -n 5 -w 1000 >nul & del /f /q "<path>\AccessibleArena_Uninstaller.exe"
```

The child process uses `CreateNoWindow=true` and `UseShellExecute=false`, so no console window flashes. After ~4–5 seconds (the delay must outlast the parent's completion MessageBox and final shutdown), `ping` returns and `del` runs. By then the parent has exited, the file lock is released, and the EXE is deleted.

**Pitfall: don't use `timeout` as the delay.** `timeout /t N /nobreak` refuses to run when stdin is not an interactive console, which is exactly what `Process.Start(CreateNoWindow=true)` gives it. It exits immediately with "ERROR: Input redirection is not supported," then `&` moves on to `del` — which fails because the parent is still alive and holding the file lock. The bug is silent: the scheduled deletion fires, returns no error, and leaves the EXE in place. `ping 127.0.0.1 -n N` has no such stdin requirement and is reliable across all Windows versions.

Same lesson applies any time you need a delay in a detached cmd: prefer `ping` over `timeout`. (Related: the in-mod auto-updater in `src/Core/Services/UpdateChecker.cs` spawns a cmd relaunch that currently uses `timeout /t 8 /nobreak` — that one runs with a visible console so `timeout` works there, but worth being aware of the asymmetry.)

### Logging Gotcha

`Logger.Flush()` is called inside `PerformUninstall` *before* `UninstallMelonLoader` runs. After MelonLoader removal finishes, `Logger.AskAndSave()` only writes a log file if `_hasErrors` is true. Consequence: on a clean successful uninstall where the user ticked the MelonLoader checkbox, the desktop log file shows only `PerformUninstall` output — no `Removing MelonLoader folder...` lines — even though the MelonLoader removal actually ran. The desktop log is therefore not a reliable diagnostic for "did MelonLoader removal happen"; check the actual file system state instead. (Worth fixing by always flushing at the true end of an uninstall run.)

## Registry Entries

Location: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AccessibleArena`

Values:
- DisplayName: "Accessible Arena"
- DisplayVersion: (mod version)
- Publisher: "Accessible Arena Project"
- InstallLocation: (MTGA path)
- InstallDate: (YYYYMMDD)
- UninstallString: `"<MtgaPath>\AccessibleArena_Uninstaller.exe" /uninstall "<MtgaPath>"` — points at the persistent copy in the MTGA folder, not the original download (see "Persistent Uninstaller in the MTGA Folder" under Uninstallation).
- QuietUninstallString: same as UninstallString, with `/quiet` appended.
- NoModify: 1
- NoRepair: 1
- EstimatedSize: 5000 (KB)
- URLInfoAbout: (GitHub repo URL)
- HelpLink: (GitHub issues URL)

## Logging

**Behavior:**
- All operations are logged to memory buffer
- On successful install: Only asks to save log if there were warnings/errors
- On failure: Always asks if user wants to save log file
- Log saved to Desktop as `AccessibleArena_Install.log`

**Rationale:** Users doing clean installs don't need log files cluttering their Desktop.

## Configuration (Config.cs)

Update these values before building for release:
```csharp
ModRepositoryUrl = "https://github.com/JeanStiletto/AccessibleArena"
ModDllName = "AccessibleArena.dll"
Publisher = "Accessible Arena Project"
DisplayName = "Accessible Arena"
```

## Command Line Arguments

```
AccessibleArenaInstaller.exe                      # Normal install (shows welcome)
AccessibleArenaInstaller.exe /uninstall           # Uninstall with UI
AccessibleArenaInstaller.exe /uninstall /quiet    # Silent uninstall
AccessibleArenaInstaller.exe "C:\path\to\MTGA"    # Install to specific path
```

## Accessibility Considerations

- All dialogs use standard Windows controls (inherently screen reader accessible)
- Progress updates via Label controls (announced by screen readers)
- Error messages in standard MessageBox (screen reader announces)
- No custom controls that might break accessibility
- Keyboard navigation works by default in WinForms

## Build Instructions

**Debug build (for testing):**
```powershell
cd installer\AccessibleArenaInstaller
dotnet build
```
Output: `bin\Debug\net472\AccessibleArenaInstaller.exe`

**Release build (for distribution):**
```powershell
dotnet build -c Release
```
Output: `bin\Release\net472\AccessibleArenaInstaller.exe`

## Testing Checklist

After installation, verify:
```powershell
# Check Tolk DLLs exist
Test-Path "C:\Program Files\Wizards of the Coast\MTGA\Tolk.dll"
Test-Path "C:\Program Files\Wizards of the Coast\MTGA\nvdaControllerClient64.dll"

# Check Mods folder exists
Test-Path "C:\Program Files\Wizards of the Coast\MTGA\Mods"

# Check registry entry
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AccessibleArena"
```

## Error Handling

All errors:
1. Logged to buffer with stack trace
2. Shown to user via MessageBox
3. User offered option to save log file

Common errors handled:
- MTGA not found (user selects folder manually)
- MTGA running (blocked with message)
- Network error downloading (continues without mod, shows manual download link)
- GitHub rate limit / repo not found (warns user, continues)
- Permission denied (shouldn't happen with admin manifest)

## Known Issues

### MelonLoader doesn't load when using "Launch MTGA after installation"

**Symptom:** User has MelonLoader already installed, skips the reinstall prompt, checks "Launch MTGA after installation", but when the game starts MelonLoader doesn't load. However, launching MTGA manually afterwards works fine.

**Cause:** The installer runs with administrator privileges (required for writing to Program Files). When `Process.Start()` launches MTGA.exe from the installer, the game inherits the elevated admin context. MelonLoader or the game may behave differently when running with unexpected admin privileges.

**Workarounds:**
1. Don't use "Launch MTGA after installation" - launch the game manually instead
2. Launch MTGA from the Start Menu or desktop shortcut after installation completes

**Possible fixes (not yet implemented):**
- Launch via Explorer: `Process.Start("explorer.exe", exePath)` - this runs the game in normal user context
- Use `CreateProcessAsUser` API to launch as the normal user
- Show a warning when the checkbox is checked explaining this limitation

**Status:** Partially mitigated by launching via MTGALauncher instead of MTGA.exe directly (see Version 1.7 changelog). The admin context issue may still apply but is less impactful since the launcher handles game updates before starting.

---

## Future Enhancements

Not yet implemented:
- Code signing (reduces Windows SmartScreen warnings)
- Checksum verification of downloads
- Fix for "Launch MTGA" admin context issue (see Known Issues)

## Implementation History

**Phase 1: Core Installer**
- Project setup with .NET Framework 4.7.2 WinForms
- Admin manifest for UAC elevation
- Tolk DLLs as embedded resources
- Basic file extraction and path detection

**Phase 2: MelonLoader Integration**
- Discovered MelonLoader installer has no CLI mode
- Implemented manual ZIP download and extraction
- Added progress reporting during download

**Phase 3: Mod Download**
- GitHub API integration for release checking
- Version comparison logic
- Download with progress reporting
- Update detection (installed vs latest)

**Phase 4: Polish**
- Add/Remove Programs registry integration
- Uninstaller with UI and quiet mode
- Command line argument parsing
- Improved error handling

**Post-Phase Improvements:**
- Welcome dialog with MTGA download option
- MelonLoader "already installed" dialog with reinstall option
- Optional logging (only saves on errors or user request)

## Version Matching (Important)

There are **three** version numbers that must stay in sync for releases:

- **GitHub tag** — the tag name from the latest release (e.g., `v0.6.9`). Fetched via GitHub API, `v` prefix stripped. This is the **source of truth** for releases.
- **Assembly version** — the version baked into the compiled DLL. Read from the installed file via `AssemblyName.GetAssemblyName()`. Used by the installer for update detection.
- **MelonInfo version** — the version string in the `[assembly: MelonInfo(...)]` attribute. Displayed to users at mod launch (e.g., "Accessible Arena v0.6.9 launched"). Accessed at runtime via `Info.Version`.

### Single Source of Truth: `Directory.Build.props`

All three are derived from a single place. `src/Directory.Build.props` defines `<ModVersion>`:

```xml
<Project>
  <PropertyGroup>
    <ModVersion>0.6.9</ModVersion>
    <Version>$(ModVersion)</Version>
  </PropertyGroup>
</Project>
```

This feeds both version numbers automatically:
- **Assembly version** — `<Version>` inherits from `<ModVersion>`, so the compiled DLL gets the right version.
- **MelonInfo version** — A build target in the csproj generates `VersionInfo.g.cs` (into `obj/`) containing `internal const string Value = "0.6.9"`. The `[assembly: MelonInfo(...)]` attribute references `VersionInfo.Value` instead of a hardcoded string.

For **CI release builds**, the GitHub Actions workflow passes `-p:ModVersion=` from the git tag, overriding the value in `Directory.Build.props`. This flows to both the assembly version and the generated MelonInfo constant automatically — no `sed` patching needed.

For **local dev builds**, just edit the one line in `Directory.Build.props`. After a release, bump it to the next version with a `-dev` suffix (e.g., `0.7.0-dev`).

### Pitfalls We Hit (Don't Repeat These)

**Pitfall 1: Missing `<Version>` in csproj**
If you don't set `<Version>`, .NET defaults the assembly version to `1.0.0.0`. If your GitHub tags are `v0.x`, the installer sees the installed DLL as version `1.0.0.0` which is numerically *higher* than any `0.x` release. Result: updates are never detected, even though the installed DLL is ancient.

`Directory.Build.props` ensures a real version is always set — don't remove it.

**Pitfall 2: Version sources out of sync**
Before `Directory.Build.props`, the csproj `<Version>` and MelonInfo string were independent and frequently drifted apart (e.g., csproj at 0.6.7, MelonInfo at 0.6.6, changelog at 0.6.9). CI masked this for releases but local builds showed wrong versions. Now both derive from `<ModVersion>` so they can't drift.

**Pitfall 3: Redundant version checks after user confirmation**
If your installer shows an "Update Available" dialog and the user clicks "Update", don't re-check versions before downloading. A second version check can reach a different conclusion (network error, race condition, version format edge case) and silently skip the download the user just asked for. Once the user confirms, download unconditionally.

### Version Normalization

The installer normalizes versions to 4 components before comparing:
- `v0.5` becomes `0.5.0.0`
- `0.5.0.0` stays `0.5.0.0`
- Pre-release suffixes stripped: `0.5.0-beta` becomes `0.5.0.0`

This means `v0.5` and `0.5.0.0` compare as equal, which is the desired behavior.

## Automated Releases with GitHub Actions (deprecated)

> **Note:** The GitHub Actions workflow (`.github/workflows/release.yml`) no longer works because the game DLLs (`Core.dll`, `Assembly-CSharp.dll`, etc.) are not in the repository. The mod must be built locally against the game DLLs installed on your machine. Use the local release script described below instead.

The workflow extracted the version from the git tag and passed it to `dotnet build` via `-p:ModVersion=...`. See `.github/workflows/release.yml` for reference — it remains in the repo but will fail if triggered.

## Local Release Script

Since the mod must be built against game DLLs that cannot be uploaded to GitHub, releases are created locally using `tools/release.ps1`.

**Usage:**
```powershell
powershell -NoProfile -File installer/release.ps1
```

No arguments needed. The script reads the version from `src/Directory.Build.props` and performs the full release automatically:

1. Reads `<ModVersion>` from `src/Directory.Build.props`
2. Pre-flight checks (clean working tree, tag doesn't exist, changelog section exists, tools available)
3. Builds mod in Release mode with correct version (`dotnet build -c Release -p:ModVersion=...`)
4. Builds installer in Release mode
5. Verifies both artifacts exist (`AccessibleArena.dll` + `AccessibleArenaInstaller.exe`)
6. Extracts release notes from `docs/CHANGELOG.md` for the matching version section
7. Creates an annotated git tag
8. Pushes the tag to remote
9. Creates the GitHub release via `gh` CLI with release notes and both artifacts

**Before running the script:**
1. Update `<ModVersion>` in `src/Directory.Build.props` to the new version
2. Add a `## vX.Y` section to `docs/CHANGELOG.md` with release notes
3. Commit both changes
4. Run the script

**Requirements:**
- `dotnet`, `git`, and `gh` CLIs must be installed and on PATH
- `gh` must be authenticated (`gh auth login`)
- Game must be installed (DLLs referenced during build)

Keep `Directory.Build.props` reasonably current for local dev builds (e.g., bump to `0.8.1-dev` after releasing `0.8`).

## In-Mod Auto-Updater

The mod includes its own update checker and updater, independent of the installer. This allows users who already have the mod installed to update without re-running the installer.

### Overview

- **Version check:** Background HTTP call to GitHub API on mod startup
- **User trigger:** F5 key from menu screens downloads and installs the update
- **Elevation:** File copy to Program Files requires UAC elevation via a minimal batch script
- **Relaunch:** Game is relaunched as the normal (non-elevated) user after the update

### Architecture

All update logic lives in a single static class: `src/Core/Services/UpdateChecker.cs`

**Fields (volatile for thread safety):**
- `_updateAvailable`, `_latestVersion` — result of the background version check
- `_checkComplete`, `_announced` — state flags for one-time announcement
- `_downloadTask`, `_downloadComplete`, `_downloadFailed`, `_downloadedPath` — download state
- `_releaseJson` — cached GitHub API response (reused for download URL extraction)

### Startup Version Check Flow

1. `OnInitializeMelon()` calls `UpdateChecker.CheckInBackground(Info.Version)` if `Settings.CheckForUpdates` is true
2. `Task.Run` performs HTTPS GET to `https://api.github.com/repos/{owner}/{repo}/releases/latest`
3. Parses `tag_name` from JSON response via regex (same pattern as installer's `GitHubClient`)
4. Compares with current version using `NormalizeVersion` (same logic as installer's `Program.NormalizeVersion`)
5. If newer: sets `_updateAvailable = true`, caches `_latestVersion`
6. On next `OnUpdate()` frame: announces once via screen reader ("Update available: vX.Y. Press F5 to update.")
7. If check fails (no internet, timeout, error): silently logs warning, no user-facing announcement

**Timeout:** 5 seconds for the version check (fast fail on bad connections).

### F5 Update Flow

1. User presses F5
2. `HandleUpdateShortcut()` checks active navigator — only allows on `LoadingScreenNavigator`, `GeneralMenuNavigator`, `AssetPrepNavigator`, or when no navigator is active (early boot). Otherwise announces "Update can only be started from menu screens."
3. If no update available: announces "No update available. You are on version X.Y."
4. Announces "Downloading update..."
5. `Task.Run` downloads the DLL asset from the GitHub release to `%TEMP%\AccessibleArena.dll`
6. On download completion (polled in `OnUpdate`): announces "Update downloaded. Restarting game..."
7. `PerformUpdate()` executes the two-step update (see below)

### Two-Step Update (Elevation + Relaunch)

The mod DLL is in `C:\Program Files\...\MTGA\Mods\`, which requires admin rights to write. The game itself runs as a normal user. This creates a challenge: we need elevation for the file copy but must relaunch the game as the normal user.

**Solution: Split into two processes.**

**Step 1 — Elevated batch for file copy:**
A minimal batch script is written to `%TEMP%\aa_update.bat` via `File.WriteAllLines()` (not string interpolation — avoids `%` and `""` escaping issues). The batch:
1. Waits for `MTGA.exe` to exit (polls `tasklist` every 2 seconds)
2. Copies the downloaded DLL to the Mods folder
3. Self-deletes

The batch is launched with `Verb = "runas"` (triggers UAC prompt) and `WindowStyle = ProcessWindowStyle.Hidden`.

**Step 2 — Non-elevated relaunch from C#:**
Before calling `Application.Quit()`, the mod spawns a non-elevated `cmd.exe` process:
```
cmd.exe /c timeout /t 8 /nobreak >nul & start "" "path\to\MTGALauncher.exe"
```
This inherits the game's normal-user token (not elevated), waits 8 seconds for the batch to finish the copy, then launches the game via the MTGA launcher.

**Why not do everything in the batch?**
Processes started from an elevated batch inherit the elevation. There is no reliable way to de-elevate from within a batch script. `explorer.exe "file.bat"` doesn't reliably execute batch files, and `start ""` from an elevated context launches elevated. The cleanest approach is to keep the batch minimal (copy only) and handle the relaunch from C# where we control the process token.

### Path Detection

Both `modsPath` and the launcher path are derived from the running assembly:
```
Assembly.GetExecutingAssembly().Location
  → C:\Program Files\...\MTGA\Mods\AccessibleArena.dll
  → Parent: Mods\        (target for copy)
  → Grandparent: MTGA\   (game root)
  → MTGA\MTGALauncher\MTGALauncher.exe  (launcher, with MTGA.exe fallback)
```
This works for both WotC and Steam installations without hardcoded paths.

### Batch Script Generation

**Important lesson:** Never generate batch scripts using C# string interpolation (`$@"..."` or `$"..."`). The interaction between C#'s `""` escaping, batch `%` variables (like `%~f0`), and nested quoting creates bugs that are extremely hard to diagnose. Instead, use `File.WriteAllLines()` with a plain string array:

```csharp
var batchLines = new[]
{
    "@echo off",
    ":wait",
    "tasklist /fi \"imagename eq MTGA.exe\" 2>nul | find /i \"MTGA.exe\" >nul",
    "if not errorlevel 1 (",
    "    timeout /t 2 /nobreak >nul",
    "    goto wait",
    ")",
    $"copy /y \"{downloadedDllPath}\" \"{targetPath}\"",
    // ... error handling ...
    $"del \"{batchPath}\""
};
File.WriteAllLines(batchPath, batchLines);
```

### Settings Integration

- `ModSettings.CheckForUpdates` (bool, default `true`) — controls whether the startup check runs
- Toggle available in mod settings menu (F2)
- F5 still works manually even when the startup check is disabled

### Adapting for Other Mods

To reuse this auto-updater for a different MelonLoader mod:

1. Copy `UpdateChecker.cs` into your mod project
2. Update the constants:
   - `GitHubApiUrl` — point to your repo's releases API
   - `ModDllAssetName` — your DLL filename
3. Add `System.Net.Http` reference to your csproj
4. Call `CheckInBackground(currentVersion)` from `OnInitializeMelon()`
5. Call `Update(announcer)` from `OnUpdate()`
6. Wire up F5 (or your preferred key) to `HandleF5(announcer)`
7. The `NormalizeVersion`, `FindLauncher`, and batch generation work unchanged for any MTGA mod

For non-MTGA Unity games, update `FindLauncher()` and the `tasklist` process name in the batch template.

## Localization

### Architecture
- **InstallerLocale** static class loads flat JSON files embedded as assembly resources
- JSON parser copied from mod's `LocaleManager` (no external dependencies)
- Fallback chain: active language → English → key name
- `OnLanguageChanged` event allows live UI updates when language is switched

### API
- `InstallerLocale.Initialize(code)` - Load locale files, set initial language
- `InstallerLocale.SetLanguage(code)` - Switch language (fires OnLanguageChanged)
- `InstallerLocale.Get(key)` - Get localized string
- `InstallerLocale.Format(key, args)` - Get localized string with format parameters

### Supported Languages (12)
en, de, fr, es, it, pt-BR, ru, pl, ja, ko, zh-CN, zh-TW

### Adding a New Language
1. Copy `Locales/en.json` to `Locales/{code}.json`
2. Translate all values (keep keys, `{0}` placeholders, and technical terms unchanged)
3. Add the language code to `LanguageDetector.SupportedLanguages` and `DisplayNames`
4. Rebuild - the wildcard `<EmbeddedResource Include="Locales\*.json" />` picks it up automatically

## Changelog

### Version 1.8
- Persistent uninstaller in the MTGA folder
  - Install now copies the running EXE to `<MtgaPath>\AccessibleArena_Uninstaller.exe`
  - Registry `UninstallString` points at that copy instead of the fragile Downloads path
  - Add/Remove Programs keeps working even after the original download is deleted or moved
- Self-deleting uninstaller
  - On a successful uninstall, the persistent EXE schedules its own deletion via a detached `cmd` child using `ping 127.0.0.1 -n 5` as the delay
  - Previously used `timeout`, which refuses to run without an interactive console (`Process.Start(CreateNoWindow=true)` doesn't provide one), causing `del` to hit a still-running parent and fail silently
- Complete MelonLoader cleanup when checkbox is ticked
  - Now also removes `version.dll.backup`, `dobby.dll.backup`
  - Now also removes `UserData/Loader.cfg` and `UserData/MelonPreferences.cfg`
  - Now removes empty `Plugins/`, `UserLibs/`, `UserData/` folders (was never called before — `MelonLoaderInstaller.Uninstall` had the logic but was unreferenced)
- Mod UserData cleanup on every uninstall
  - `UserData/AccessibleArena.json` and `UserData/AccessibleArena/` now removed unconditionally, not gated on the MelonLoader checkbox

### Version 1.7
- Launch MTGA via launcher instead of game executable
  - Previously launched `MTGA.exe` directly, which skipped the game's update process
  - If the game had a pending update, it would fail to start or crash
  - Now launches `MTGALauncher\MTGALauncher.exe`, which checks for and applies game updates before starting
  - Falls back to `MTGA.exe` if the launcher executable is not found

### Version 1.6
- Hide MelonLoader console window by default
  - New `ConfigureMelonLoaderConsole()` method in InstallationManager
  - Sets `hide_console = true` in `UserData/Loader.cfg` during installation
  - Handles all cases: existing config, missing entry, or no config file yet
  - Runs for both fresh installs and updates

### Version 1.5
- Launch announcement now shows mod name and version ("Accessible Arena v0.6.9 launched")
- MelonInfo version updated from placeholder "0.1.0-beta" to current version
- MelonInfo now reads from auto-generated `VersionInfo.Value` constant (derived from `Directory.Build.props`)
  - CI passes `-p:ModVersion=` to override at build time — no `sed` patching needed
  - Ensures `Info.Version` (runtime) matches the release tag alongside the assembly version

### Version 1.4
- Full installer localization with 12 languages
  - InstallerLocale static class with embedded JSON resources
  - All user-facing strings replaced with localized calls
  - Language auto-detected from OS, changeable in welcome wizard
  - Live language switching updates all form controls
- Two-page welcome wizard
  - Page 1: Mod description, version to install, language selector, Next
  - Page 2: MTGA download links, Back, Install
- Fixed version detection using registry instead of DLL assembly version
  - Registry stores GitHub release tag after install (source of truth)
  - Falls back to DLL assembly version only if no registry entry
  - Prevents perpetual "update available" when DLL has stale assembly version
- Removed redundant version check in MainForm during full install/reinstall
  - User already confirmed in Program.cs, no second prompt needed
- Added "Mod Up to Date" dialog when mod is current
  - Shows installed version, offers Close or Full Reinstall
- GitHub version always fetched at startup (shown on welcome page)

### Version 1.3
- Fixed update detection failing for pre-v0.5 DLLs with legacy 1.0.0.0 assembly version
  - NormalizeVersion now treats 1.0.0.0 (the .NET default) as 0.0.0.0 so any real release is newer
- Fixed redundant version check in update mode blocking the download
  - When user confirmed update via Update Available dialog, MainForm no longer re-checks versions
- Fixed initial path detection using hardcoded default instead of DetectMtgaPath()
  - Non-default MTGA installs now correctly detected for update checking
- Fixed GitHub Actions workflow not setting DLL version from tag
  - Workflow now extracts version from tag name and passes `-p:Version=` to dotnet build
  - No longer depends on manually bumping `<Version>` in csproj before tagging

### Version 1.2
- Fixed update check: version comparison now normalizes both sides to 4 components
- Fixed mod always reporting version 1.0.0.0 (missing `<Version>` in csproj)
- Added GitHub Actions workflow for automated release builds

### Version 1.1
- Added welcome confirmation dialog at startup
- Added automatic update check on launch (compares installed vs GitHub version)
- Added Update Available dialog with Update/Full Install/Close options
- Added quick update mode (skips MelonLoader/Tolk, only updates mod DLL)
- WelcomeForm: Added "Direct Download" button for MTGA installer
- WelcomeForm: Renamed download button to "Download Page"
- MainForm: Support for update-only mode with appropriate UI changes
- Documented known issue: MelonLoader not loading when using auto-launch

### Version 1.0
- Initial release
- Full installation of MelonLoader, Tolk DLLs, and mod
- Uninstaller with Add/Remove Programs integration
- Welcome dialog with MTGA download option
- Optional logging
