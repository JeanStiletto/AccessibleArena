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
    $mtgaNode = $xml.SelectSingleNode("//MtgaPath")
    $overridePath = if ($mtgaNode) { $mtgaNode.InnerText } else { $null }
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

    # ilspycmd writes decompiler warnings to stderr and exits non-zero even when it produced
    # perfectly good code (HomePageContentController is one such type). Merging stderr into the
    # output under $ErrorActionPreference = "Stop" turned that into a terminating error, so the
    # type looked undecompilable. Keep the two streams apart and judge by what came out.
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $raw = & $ilspycmd $dllPath -t $TypeName 2>&1
    } finally {
        $ErrorActionPreference = $previousEap
    }

    $codeLines = @($raw | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })
    $errLines = @($raw | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } |
        ForEach-Object { $_.ToString() })
    $outputStr = ($codeLines -join [Environment]::NewLine)
    $errStr = ($errLines -join [Environment]::NewLine)

    # A type declaration is the proof that decompilation worked. Bare `using` lines and a
    # `namespace` header alone are what a miss looks like.
    if ($outputStr -match "\b(class|struct|enum|interface|record|delegate)\b") {
        $outputStr | Out-File -Encoding utf8 $outFile
        Write-Host "  [OK] Decompiled '$TypeName' from $dllKey -> $outFile" -ForegroundColor Green
        if ($errStr) {
            Write-Host "  [NOTE] ilspycmd also reported: $($errStr.Split([Environment]::NewLine)[0])" -ForegroundColor DarkGray
        }
        $success = $true
        break
    }

    # Parse "not found in module but only in X" to suggest the correct DLL
    if ($errStr -match "only in (\w+)") {
        $hint = $matches[1]
        Write-Host "  [MISS] $dllKey - type is in '$hint' assembly instead" -ForegroundColor Yellow
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
        Write-Host "  [MISS] No code output from $dllKey" -ForegroundColor Yellow
        if ($dllKey -eq $searchOrder[-1] -and $errStr) {
            Write-Host "  Last attempt error: $($errStr.Substring(0, [Math]::Min(200, $errStr.Length)))" -ForegroundColor DarkGray
        }
    }
}

if (-not $success) {
    Write-Host "`n  [FAIL] Could not decompile '$TypeName' from any DLL." -ForegroundColor Red
    Write-Host "  Tried: $($searchOrder -join ', ')" -ForegroundColor Red
    Write-Host "  Hint: Check the full namespace. Use type-index.md for known mappings." -ForegroundColor Yellow
    exit 1
}

# Explicit, so decompile-all.ps1's $LASTEXITCODE check sees this script's result rather than
# ilspycmd's, which is non-zero even on a successful decompile.
exit 0
