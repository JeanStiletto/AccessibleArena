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
installer/AccessibleArenaInstaller/
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
6. **Register in Add/Remove Programs** - stores GitHub release tag as version

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

**Trigger methods:**
- Windows Settings → Apps → Accessible Arena → Uninstall
- Control Panel → Programs and Features
- Command line: `AccessibleArenaInstaller.exe /uninstall`
- Silent: `AccessibleArenaInstaller.exe /uninstall /quiet`

**What gets removed:**
- AccessibleArena.dll from Mods folder
- Tolk.dll and nvdaControllerClient64.dll from MTGA root
- Backup files (.backup)
- Empty Mods folder (if no other mods)
- Registry uninstall entry

**Optional:** User can choose to also remove MelonLoader (checkbox in UninstallForm)

## Registry Entries

Location: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AccessibleArena`

Values:
- DisplayName: "Accessible Arena"
- DisplayVersion: (mod version)
- Publisher: "Accessible Arena Project"
- InstallLocation: (MTGA path)
- InstallDate: (YYYYMMDD)
- UninstallString: (path to installer with /uninstall flag)
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

**Status:** Documented, not yet fixed. Users should launch MTGA manually after installation.

---

## Future Enhancements

Not yet implemented:
- In-mod update checker (notify user on game launch if update available)
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

The installer compares two version numbers to detect updates:

- **GitHub tag** — the tag name from the latest release (e.g., `v0.6.5`). Fetched via GitHub API, `v` prefix stripped.
- **Assembly version** — the version baked into the compiled DLL. Read from the installed file via `AssemblyName.GetAssemblyName()`.

These two are completely independent sources. If they get out of sync, update detection breaks silently. The GitHub Actions workflow below handles this automatically, but if you build manually or adapt this for your own project, read the pitfalls section.

### Pitfalls We Hit (Don't Repeat These)

**Pitfall 1: Missing `<Version>` in csproj**
If you don't set `<Version>` in your `.csproj`, .NET defaults the assembly version to `1.0.0.0`. If your GitHub tags are `v0.x`, the installer sees the installed DLL as version `1.0.0.0` which is numerically *higher* than any `0.x` release. Result: updates are never detected, even though the installed DLL is ancient.

The installer now treats `1.0.0.0` as `0.0.0.0` as a safety net (see `NormalizeVersion`), but don't rely on this. Always set a real version.

**Pitfall 2: Forgetting to bump the csproj version**
Even after adding `<Version>`, if you tag `v0.6.5` on GitHub but the csproj still says `<Version>0.5.0</Version>`, every DLL you build will report `0.5.0.0` regardless of the release tag. The installer will perpetually show "update available" even when users already have the latest version, or worse, will skip updates when the stale version happens to match.

**Solution:** Use the GitHub Actions workflow below, which injects the version from the git tag at build time. This makes the csproj version a fallback for local dev builds only - release builds always get the correct version from the tag.

**Pitfall 3: Redundant version checks after user confirmation**
If your installer shows an "Update Available" dialog and the user clicks "Update", don't re-check versions before downloading. A second version check can reach a different conclusion (network error, race condition, version format edge case) and silently skip the download the user just asked for. Once the user confirms, download unconditionally.

### Version Normalization

The installer normalizes versions to 4 components before comparing:
- `v0.5` becomes `0.5.0.0`
- `0.5.0.0` stays `0.5.0.0`
- `1.0.0.0` (the .NET default) is treated as `0.0.0.0`
- Pre-release suffixes stripped: `0.5.0-beta` becomes `0.5.0.0`

This means `v0.5` and `0.5.0.0` compare as equal, which is the desired behavior.

## Automated Releases with GitHub Actions

The release workflow extracts the version from the git tag and passes it to `dotnet build` via `-p:Version=...`. This overrides whatever `<Version>` is in the csproj, so the DLL assembly version always matches the tag automatically.

Create `.github/workflows/release.yml` in your repository:

```yaml
name: Release

on:
  push:
    tags: ['v*']

jobs:
  build-and-release:
    runs-on: windows-latest
    permissions:
      contents: write

    steps:
      - uses: actions/checkout@v4

      - name: Extract version from tag
        id: version
        shell: bash
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - name: Build mod (Release)
        run: dotnet build src/YourMod.csproj -c Release -p:Version=${{ steps.version.outputs.VERSION }}

      - name: Build installer (Release)
        run: dotnet build installer/YourInstaller.csproj -c Release

      - name: Upload release assets
        uses: softprops/action-gh-release@v2
        with:
          files: |
            src/bin/Release/net472/YourMod.dll
            installer/bin/Release/net472/YourInstaller.exe
```

The key line is `-p:Version=${{ steps.version.outputs.VERSION }}` on the mod build step. This strips the `v` prefix from the tag (e.g., `v0.6.5` becomes `0.6.5`) and passes it as the assembly version.

**Release workflow:**
1. Tag: `git tag v0.6.5`
2. Push: `git push origin v0.6.5`
3. GitHub Actions builds with the correct version baked in - no manual csproj edits needed

You should still set a reasonable `<Version>` in the csproj for local development builds (it serves as a fallback when not building via CI), but the CI workflow is the source of truth for release versions.

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
