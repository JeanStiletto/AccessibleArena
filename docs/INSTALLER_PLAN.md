# Accessible Arena Installer - Implementation Plan

## Overview

Single-file C# WinForms installer that:
- Uses standard Windows dialogs (screen reader accessible)
- Installs MelonLoader + mod + dependencies
- Supports updates and uninstallation
- Logs installation for troubleshooting

## Technology Stack

- **Language:** C# (.NET Framework 4.7.2 or 4.8)
- **UI Framework:** WinForms (native Windows dialogs)
- **Single-file packaging:** Costura.Fody (embeds DLLs into EXE)
- **Admin rights:** Application manifest requesting `requireAdministrator`
- **HTTP client:** System.Net.Http for GitHub API calls

## File Delivery Strategy (Hybrid)

**Embedded in installer:**
- Tolk.dll
- nvdaControllerClient64.dll (and 32-bit if needed)
- SAAPI64.dll (if using SAPI fallback)

**Downloaded at install time:**
- MelonLoader (from official GitHub releases)
- AccessibleArena.dll (from your GitHub releases)

**Rationale:** Tolk DLLs rarely change, MelonLoader updates frequently, your mod updates with releases.

## Installation Flow

### Step 1: Pre-flight Checks
1. Check if running as admin (should be via manifest)
2. Check if MTGA.exe is running → Block with message: "Please close Magic: The Gathering Arena before installing."
3. Detect MTGA installation path:
   - Check default: `C:\Program Files\Wizards of the Coast\MTGA`
   - Verify by looking for `MTGA.exe`
   - If not found or user wants different location: Show `FolderBrowserDialog`

### Step 2: Version Check (if mod already installed)
1. Check if `Mods\AccessibleArena.dll` exists
2. If exists, read assembly version
3. Fetch latest version from GitHub API
4. Show dialog: "Accessible Arena is already installed. Installed: v1.0.0, Latest: v1.1.0. Reinstall/Update?"
5. Options: [Update] [Cancel]

### Step 3: MelonLoader Installation
1. Check if MelonLoader already installed (look for `MelonLoader` folder and `version.dll`)
2. If not installed or outdated:
   - Download MelonLoader.Installer.exe from `https://github.com/LavaGang/MelonLoader/releases/latest`
   - Run silently: `MelonLoader.Installer.exe --game "C:\...\MTGA" --version latest --auto`
   - Wait for completion
   - Verify installation succeeded
3. If already installed: Skip or offer reinstall option

### Step 4: Copy Mod Files
1. Wait for MelonLoader to create `Mods` folder (may need short delay after ML install)
2. Copy embedded Tolk DLLs to MTGA root folder:
   - `Tolk.dll`
   - `nvdaControllerClient64.dll`
   - Other NVDA dependencies
3. Download latest AccessibleArena.dll from GitHub releases
4. Copy to `Mods\AccessibleArena.dll`

### Step 5: Registry (Uninstaller)
Create registry entries for Add/Remove Programs:
```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AccessibleArena
  DisplayName = "Accessible Arena"
  DisplayVersion = "1.0.0"
  Publisher = "Accessible Arena"
  InstallLocation = "C:\Program Files\Wizards of the Coast\MTGA"
  UninstallString = "path\to\uninstaller.exe"
  NoModify = 1
  NoRepair = 1
```

### Step 6: Completion
1. Write installation log to `%USERPROFILE%\Desktop\AccessibleArena_Install.log`
2. Show success dialog: "Installation complete! You can now launch MTGA."
3. Offer to open log file if errors occurred

## Uninstallation Flow

Separate uninstaller EXE (or same EXE with `/uninstall` flag):
1. Remove `Mods\AccessibleArena.dll`
2. Remove Tolk DLLs from MTGA root (Tolk.dll, nvdaControllerClient64.dll)
3. Optionally remove MelonLoader (ask user - they might have other mods)
4. Remove registry uninstall entry
5. Show completion message

## Update Check Feature (Future - In-Mod)

For later implementation in the mod itself:
1. On game launch (after delay), check GitHub API for latest release
2. Compare with embedded version string
3. If update available: `Tolk.Speak("Update available for Accessible Arena. Version X.Y.Z. Press F12 to open download page.")`
4. F12 hotkey opens browser to releases page

## Project Structure

```
installer/
├── AccessibleArenaInstaller/
│   ├── AccessibleArenaInstaller.csproj
│   ├── app.manifest              # UAC elevation request
│   ├── Program.cs                # Entry point
│   ├── MainForm.cs               # Main installer UI
│   ├── InstallationManager.cs    # Core install logic
│   ├── MelonLoaderInstaller.cs   # MelonLoader download/install
│   ├── GitHubClient.cs           # GitHub API for downloads
│   ├── RegistryManager.cs        # Add/Remove Programs
│   ├── Logger.cs                 # Installation logging
│   ├── FodyWeavers.xml           # Costura configuration
│   └── Resources/
│       ├── Tolk.dll              # Embedded
│       ├── nvdaControllerClient64.dll
│       └── ...other NVDA DLLs
└── README.md
```

## UI Screens (All Standard Windows Dialogs)

### Screen 1: Welcome
- MessageBox or simple Form with:
  - "Accessible Arena Installer"
  - "This will install the accessibility mod for Magic: The Gathering Arena."
  - [Install] [Cancel]

### Screen 2: Folder Selection (if needed)
- `FolderBrowserDialog` with description: "Select MTGA installation folder"
- Pre-selected: Default path or detected path

### Screen 3: Progress
- Simple form with:
  - Label showing current step
  - Optional: ProgressBar (accessible via screen reader)
  - Steps announced via accessible labels

### Screen 4: Completion
- MessageBox: "Installation complete!" or error details

## Error Handling

All errors should:
1. Be logged to install log file
2. Show accessible MessageBox with clear description
3. Offer option to view log file

Common errors to handle:
- MTGA not found
- MTGA running (files locked)
- Network error downloading files
- MelonLoader installer failed
- Permission denied (shouldn't happen with admin manifest)
- GitHub rate limit exceeded

## Code Signing (Optional)

For avoiding Windows SmartScreen warnings:
- **Phase 1:** Release unsigned, document the "More info → Run anyway" workaround
- **Phase 2:** If user adoption grows, consider SignPath.io (free for open source)
- Note: SmartScreen reputation builds over time with downloads

## Implementation Order

**Phase 1: Core Installer**
1. Create project with Costura.Fody
2. Add app.manifest for admin rights
3. Implement folder detection and selection
4. Implement file copying (Tolk DLLs)
5. Test basic installation

**Phase 2: MelonLoader Integration**
6. Implement GitHub API client
7. Download and run MelonLoader installer silently
8. Verify MelonLoader installation

**Phase 3: Mod Download**
9. Download mod DLL from GitHub releases
10. Copy to Mods folder
11. Version checking

**Phase 4: Polish**
12. Add uninstaller and registry entries
13. Add logging
14. Error handling improvements
15. Test full flow

**Phase 5: Future Enhancement**
16. In-mod update checker

## Dependencies to Research

Before implementation:
- [ ] MelonLoader silent install command-line arguments (verify `--auto` flag exists)
- [ ] GitHub releases API response format for your repo
- [ ] Exact list of Tolk/NVDA DLLs needed
- [ ] Whether MelonLoader creates Mods folder immediately or on first game launch

## Accessibility Considerations

- All dialogs use standard Windows controls (inherently accessible)
- Progress updates via accessible Label controls
- Error messages in standard MessageBox (screen reader announces)
- No custom controls that might break screen reader compatibility
- Keyboard navigation works by default in WinForms

## Resolved Decisions

1. **Launch after install:** Yes - offer checkbox "Launch MTGA now" on completion screen
2. **Outdated MelonLoader:** Ask user - "MelonLoader appears outdated. Update to latest version? (Recommended)"
3. **Backup:** Yes - copy existing AccessibleArena.dll to AccessibleArena.dll.backup before overwriting

## Remaining Questions for Implementation

1. Exact MelonLoader CLI arguments for silent install (needs research)
2. Desktop shortcut - probably not needed (game already has one)
