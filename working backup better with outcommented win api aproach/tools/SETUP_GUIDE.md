# MTGA Accessibility Mod - Setup Guide

## Required Tools

### 1. MelonLoader (Mod Framework)
- Purpose: Loads mods into Unity games
- Download: https://github.com/LavaGang/MelonLoader/releases
- Installation: Run the installer and select MTGA.exe

### 2. ILSpy (Code Decompiler)
- Purpose: View and analyze game assemblies
- Download: https://github.com/icsharpcode/ILSpy/releases
- Usage: Open DLL files from MTGA_Data\Managed folder

### 3. dnSpy (Alternative Decompiler)
- Purpose: Advanced decompilation with debugging
- Download: https://github.com/dnSpy/dnSpy/releases
- Note: More features but larger download

### 4. Visual Studio or VS Code
- Purpose: Write mod code
- Required: .NET development workload

### 5. Tolk Library
- Purpose: Screen reader communication
- Download: https://github.com/ndarilek/tolk
- Supports: NVDA, JAWS, and other screen readers

## Project Dependencies

### NuGet Packages for Mod Development
- MelonLoader.ModHelper
- UnhollowerBaseLib (for Il2Cpp games, if applicable)

### Reference Assemblies (Copy from game)
From MTGA_Data\Managed, copy to libs folder:
- Assembly-CSharp.dll
- UnityEngine.dll
- UnityEngine.CoreModule.dll
- UnityEngine.UI.dll
- Unity.TextMeshPro.dll

## Installation Steps

### Step 1: Install MelonLoader
1. Download MelonLoader installer
2. Run installer
3. Select MTGA.exe at: C:\Program Files\Wizards of the Coast\MTGA\MTGA.exe
4. Click Install
5. Run MTGA once to generate MelonLoader folders

### Step 2: Verify MelonLoader Installation
After first run, check for these folders:
- MTGA\MelonLoader folder
- MTGA\Mods folder
- MTGA\Plugins folder

### Step 3: Set Up Development Environment
1. Create new C# Class Library project
2. Target .NET Framework 4.7.2 or matching game version
3. Add references to copied assemblies in libs folder
4. Add MelonLoader assembly references

### Step 4: Analyze Game Code
1. Open ILSpy
2. Load Assembly-CSharp.dll
3. Search for UI and text-related classes
4. Document relevant methods for hooking

## File Locations Summary

Game install: C:\Program Files\Wizards of the Coast\MTGA
Game assemblies: MTGA_Data\Managed
Mod output: MTGA\Mods (after MelonLoader install)
Project folder: C:\Users\fabia\arena

## Next Steps After Setup

1. Decompile Assembly-CSharp.dll with ILSpy
2. Find card text display classes
3. Find UI navigation classes
4. Create basic mod that logs card information
5. Add Tolk integration for screen reader output
