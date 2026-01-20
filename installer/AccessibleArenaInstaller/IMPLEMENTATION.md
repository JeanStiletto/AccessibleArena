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
├── Program.cs                           # Entry point, CLI args, uninstall logic
├── WelcomeForm.cs                       # Welcome dialog (first screen)
├── MainForm.cs                          # Main installer UI
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

### Step 1: Welcome Dialog
- Shows mod name and description
- Offers "Download MTGA" button (opens browser) for users without MTGA
- "Install Mod" button proceeds to installation

### Step 2: Pre-flight Checks (in Program.cs)
1. Check if running as admin (via manifest, should always be true)
2. Check if MTGA.exe is running → Block with message
3. Detect MTGA installation path:
   - Check registry for previous install location
   - Check default: `C:\Program Files\Wizards of the Coast\MTGA`
   - Check x86 Program Files as fallback
   - If not found: User selects via FolderBrowserDialog

### Step 3: Main Installation (MainForm.cs)
1. **Copy Tolk DLLs** - Extract embedded resources to MTGA root
2. **MelonLoader Check/Install:**
   - If not installed: Ask user, then download ZIP and extract
   - If already installed: Ask if user wants to reinstall or keep existing
3. **Create Mods folder** if it doesn't exist
4. **Download Mod DLL:**
   - Check installed version vs GitHub latest
   - If update available: Ask user
   - Download and install to Mods folder
5. **Register in Add/Remove Programs** (registry)

### Step 4: Completion
- Show success message with first-launch warning (if MelonLoader was installed)
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

## Future Enhancements

Not yet implemented:
- Language localization (currently English only)
- In-mod update checker (notify user on game launch if update available)
- Code signing (reduces Windows SmartScreen warnings)
- Checksum verification of downloads

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
