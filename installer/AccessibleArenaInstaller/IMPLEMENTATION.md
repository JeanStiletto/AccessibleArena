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
- **File Embedding:** Standard EmbeddedResource (for Tolk DLLs)
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
├── WelcomeForm.cs                       # Welcome dialog with download options
├── UpdateAvailableForm.cs               # Update available dialog (update/full install/close)
├── MainForm.cs                          # Main installer/updater UI
├── UninstallForm.cs                     # Uninstall UI
├── InstallationManager.cs               # Core file operations
├── MelonLoaderInstaller.cs              # MelonLoader download/extraction
├── GitHubClient.cs                      # GitHub API for downloads
├── RegistryManager.cs                   # Add/Remove Programs registry
├── Config.cs                            # Configuration constants
├── Logger.cs                            # Optional installation logging
└── Resources/
    ├── Tolk.dll                         # Embedded
    └── nvdaControllerClient64.dll       # Embedded
```

## Installation Flow

### Step 1: Pre-flight Checks (in Program.cs)
1. Check if running as admin (via manifest, should always be true)
2. Check if MTGA.exe is running → Block with message

### Step 2: Update Check
1. Check if mod DLL exists in default MTGA location
2. If exists: Compare installed version with latest GitHub version
3. If update available: Show **Update Available Dialog** with options:
   - **Update Mod** - Quick update (skips MelonLoader/Tolk, only updates mod DLL)
   - **Full Install** - Proceeds to full installation flow
   - **Close** - Exit installer
4. If no mod or no update: Show welcome confirmation dialog

### Step 3: Welcome Confirmation
- Shows "Welcome to Accessible Arena!" message
- User confirms with OK to proceed, or Cancel to exit

### Step 4: Welcome Dialog
- Shows mod description and MTGA download options
- Three buttons:
  - **Direct Download** - Downloads MTGAInstaller.exe directly
  - **Download Page** - Opens MTGA website
  - **Install Mod** - Proceeds to installation

### Step 5: Path Detection
- Check registry for previous install location
- Check default: `C:\Program Files\Wizards of the Coast\MTGA`
- Check x86 Program Files as fallback
- If not found: User selects via FolderBrowserDialog

### Step 6: Main Installation (MainForm.cs)
**Full Install Mode:**
1. **Copy Tolk DLLs** - Extract embedded resources to MTGA root
2. **MelonLoader Check/Install:**
   - If not installed: Ask user, then download ZIP and extract
   - If already installed: Ask if user wants to reinstall or keep existing
3. **Create Mods folder** if it doesn't exist
4. **Download Mod DLL** from GitHub releases
5. **Register in Add/Remove Programs** (registry)

**Update Only Mode:**
1. Skip Tolk DLLs and MelonLoader
2. **Download Mod DLL** from GitHub releases
3. **Update registry** with new version

### Step 7: Completion
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
- Language localization (currently English only)
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

- **GitHub tag** — the tag name you type when creating a release (e.g., `v0.5`). Fetched via GitHub API.
- **Assembly version** — the version baked into the compiled DLL. Read from the installed file via `AssemblyName.GetAssemblyName()`.

These are completely independent. The GitHub tag is just a string, the assembly version comes from the `<Version>` property in your `.csproj` file. If you don't set `<Version>`, .NET defaults to `1.0.0.0`, which will always look "newer" than any `0.x` tag — so the installer will never detect updates.

**To avoid this:**
- Always set `<Version>` in your mod's `.csproj`:
  ```xml
  <PropertyGroup>
    <Version>0.5.0</Version>
  </PropertyGroup>
  ```
- Keep it in sync with your GitHub release tags. When you tag `v0.5`, the csproj should say `0.5.0`.
- The installer normalizes versions to 4 components (e.g., `0.5` becomes `0.5.0.0`), so `v0.5` and `0.5.0.0` compare as equal.

## Automated Releases with GitHub Actions

Instead of manually building and uploading release assets, you can use GitHub Actions to automate this. Create `.github/workflows/release.yml` in your repository:

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

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - name: Build mod (Release)
        run: dotnet build src/YourMod.csproj -c Release

      - name: Build installer (Release)
        run: dotnet build installer/YourInstaller.csproj -c Release

      - name: Upload release assets
        uses: softprops/action-gh-release@v2
        with:
          files: |
            src/bin/Release/net472/YourMod.dll
            installer/bin/Release/net472/YourInstaller.exe
```

**Release workflow becomes:**
1. Bump `<Version>` in your `.csproj`, commit
2. Tag: `git tag v0.5`
3. Push: `git push origin v0.5`

GitHub builds both projects and attaches the DLL and installer EXE to the release automatically. No more forgotten assets.

## Changelog

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
