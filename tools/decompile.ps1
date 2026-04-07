# Decompile a game type using ilspycmd
# Usage: powershell -NoProfile -File tools\decompile.ps1 [-TypeName] "Namespace.TypeName" [-Dll Core|Asm|Gre|Auto] [-OutDir path]
#
# Examples:
#   .\tools\decompile.ps1 "Core.Meta.MainNavigation.Store.StoreSetFilterToggles"
#   .\tools\decompile.ps1 "ContentController_StoreCarousel" -Dll Core
#   .\tools\decompile.ps1 "StopType" -Dll Gre
#   .\tools\decompile.ps1 "SomeType" -Dll Auto    # tries Core, then Asm, then Gre
#
# Output goes to llm-docs/decompiled/<SafeTypeName>.cs by default

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TypeName,

    [Parameter(Position=1)]
    [ValidateSet("Core", "Asm", "Gre", "Shared", "Auto")]
    [string]$Dll = "Auto",

    [Parameter()]
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

# Ensure ilspycmd can find .NET 8.0 runtime installed in user-local directory
$userDotnet = "$env:USERPROFILE\AppData\Local\Microsoft\dotnet"
if ((Test-Path $userDotnet) -and -not $env:DOTNET_ROOT) {
    $env:DOTNET_ROOT = $userDotnet
}

# Paths - detect game install (prefer local.props override, then WotC, then Steam)
$localPropsPath = Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) "src\local.props"
$managedDir = $null

if (Test-Path $localPropsPath) {
    $xml = [xml](Get-Content $localPropsPath)
    $overridePath = $xml.SelectSingleNode("//MtgaPath")?.InnerText
    if ($overridePath) {
        $managedDir = "$overridePath\MTGA_Data\Managed"
        Write-Host "  [INFO] Using MtgaPath from local.props: $overridePath" -ForegroundColor DarkCyan
    }
}

if (-not $managedDir -or -not (Test-Path $managedDir)) {
    $wotcPath = "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed"
    $steamPath = "C:\Program Files (x86)\Steam\steamapps\common\MTGA\MTGA_Data\Managed"
    if (Test-Path $wotcPath) {
        $managedDir = $wotcPath
    } elseif (Test-Path $steamPath) {
        $managedDir = $steamPath
        Write-Host "  [INFO] WotC path not found, using Steam install" -ForegroundColor DarkCyan
    } else {
        Write-Error "MTGA managed directory not found. Create src/local.props with the correct MtgaPath."
        exit 1
    }
}
$ilspycmd = "$env:USERPROFILE\.dotnet\tools\ilspycmd.exe"
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $repoRoot "llm-docs\decompiled"
}

# Ensure output directory exists
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

# DLL map
$dllMap = @{
    "Core"   = Join-Path $managedDir "Core.dll"
    "Asm"    = Join-Path $managedDir "Assembly-CSharp.dll"
    "Gre"    = Join-Path $managedDir "Wizards.MDN.GreProtobuf.dll"
    "Shared" = Join-Path $managedDir "SharedClientCore.dll"
}

# Build search order
if ($Dll -eq "Auto") {
    $searchOrder = @("Core", "Asm", "Gre", "Shared")
} else {
    $searchOrder = @($Dll)
}

# Safe filename from type name (replace dots and special chars)
$safeFileName = $TypeName -replace '[^a-zA-Z0-9_.]', '_'
# Use just the last segment for shorter filenames
$shortName = $TypeName.Split('.')[-1]
$outFile = Join-Path $OutDir "$shortName.cs"

$success = $false

foreach ($dllKey in $searchOrder) {
    $dllPath = $dllMap[$dllKey]

    if (-not (Test-Path $dllPath)) {
        Write-Host "  [SKIP] $dllKey - DLL not found at $dllPath" -ForegroundColor Yellow
        continue
    }

    Write-Host "  [TRY] $dllKey ($dllPath) for type '$TypeName'..." -ForegroundColor Cyan

    try {
        $output = & $ilspycmd $dllPath -t $TypeName 2>&1
        $outputStr = $output | Out-String

        # Check for common failure patterns
        if ($outputStr -match "Error" -and $outputStr -match "not find") {
            Write-Host "  [MISS] Type not found in $dllKey" -ForegroundColor Yellow
            continue
        }

        # Check if we got actual code (has class/struct/enum/interface keyword)
        if ($outputStr -match "\b(class|struct|enum|interface|namespace)\b") {
            $outputStr | Out-File -Encoding utf8 $outFile
            Write-Host "  [OK] Decompiled '$TypeName' from $dllKey -> $outFile" -ForegroundColor Green
            $success = $true
            break
        } else {
            Write-Host "  [MISS] No code output from $dllKey" -ForegroundColor Yellow
            # Save error output for debugging if it's the last attempt
            if ($dllKey -eq $searchOrder[-1]) {
                Write-Host "  Last attempt output: $($outputStr.Substring(0, [Math]::Min(200, $outputStr.Length)))" -ForegroundColor DarkGray
            }
        }
    } catch {
        $errMsg = $_.Exception.Message
        # Parse "not found in module but only in X" to suggest correct DLL
        if ($errMsg -match "only in (\w+)") {
            $hint = $matches[1]
            Write-Host "  [MISS] $dllKey - type is in '$hint' assembly instead" -ForegroundColor Yellow
            # If in Auto mode and the hint maps to a known DLL key, try it next
            if ($Dll -eq "Auto") {
                $hintKey = $null
                if ($hint -eq "Core") { $hintKey = "Core" }
                elseif ($hint -eq "Assembly-CSharp") { $hintKey = "Asm" }
                elseif ($hint -match "SharedClientCore") { $hintKey = "Shared" }
                elseif ($hint -match "GreProtobuf") { $hintKey = "Gre" }
                if ($hintKey -and $searchOrder -notcontains $hintKey) {
                    $searchOrder += $hintKey
                }
            }
        } else {
            Write-Host "  [ERR] $dllKey failed: $errMsg" -ForegroundColor Red
        }
    }
}

if (-not $success) {
    Write-Host "`n  [FAIL] Could not decompile '$TypeName' from any DLL." -ForegroundColor Red
    Write-Host "  Tried: $($searchOrder -join ', ')" -ForegroundColor Red
    Write-Host "  Hint: Check the full namespace. Use type-index.md for known mappings." -ForegroundColor Yellow
    exit 1
}
