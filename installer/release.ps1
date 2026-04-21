# Accessible Arena — Local Release Script
# Usage: powershell -NoProfile -File installer/release.ps1
#
# Reads the version from src/Directory.Build.props, builds mod + installer,
# extracts changelog notes, creates a git tag, and publishes a GitHub release.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

# ── 1. Read version from Directory.Build.props ──────────────────────────────

$propsFile = Join-Path $root 'src\Directory.Build.props'
if (-not (Test-Path $propsFile)) {
    Write-Host "ERROR: $propsFile not found" -ForegroundColor Red
    exit 1
}

[xml]$props = Get-Content $propsFile
$version = $props.Project.PropertyGroup.ModVersion
if (-not $version) {
    Write-Host "ERROR: ModVersion not found in $propsFile" -ForegroundColor Red
    exit 1
}

$tag = "v$version"
Write-Host "Releasing $tag" -ForegroundColor Cyan

# ── 2. Pre-flight checks ────────────────────────────────────────────────────

# Check tools
if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: dotnet CLI not found" -ForegroundColor Red; exit 1
}
if (-not (Get-Command 'gh' -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: gh CLI not found (install from https://cli.github.com)" -ForegroundColor Red; exit 1
}
if (-not (Get-Command 'git' -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: git not found" -ForegroundColor Red; exit 1
}

# Check gh auth
$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: gh CLI not authenticated. Run 'gh auth login' first." -ForegroundColor Red; exit 1
}

# Check no uncommitted changes to tracked files (untracked files are OK)
$gitDirty = git diff --stat HEAD
if ($gitDirty) {
    Write-Host "ERROR: Uncommitted changes to tracked files. Commit or stash first." -ForegroundColor Red
    git diff --stat HEAD
    exit 1
}

# Check tag doesn't exist
$existingTag = git tag -l $tag
if ($existingTag) {
    Write-Host "ERROR: Tag $tag already exists" -ForegroundColor Red; exit 1
}

# Check changelog section exists
$changelogFile = Join-Path $root 'docs\CHANGELOG.md'
if (-not (Test-Path $changelogFile)) {
    Write-Host "ERROR: $changelogFile not found" -ForegroundColor Red; exit 1
}

$changelogContent = Get-Content $changelogFile -Raw
if ($changelogContent -notmatch "(?m)^## $([regex]::Escape($tag))") {
    Write-Host "ERROR: No '## $tag' section found in docs/CHANGELOG.md" -ForegroundColor Red; exit 1
}

Write-Host "Pre-flight checks passed" -ForegroundColor Green

# ── 3. Build mod ─────────────────────────────────────────────────────────────

Write-Host "`nBuilding mod (Release)..." -ForegroundColor Cyan
$modCsproj = Join-Path $root 'src\AccessibleArena.csproj'
dotnet build $modCsproj -c Release "-p:ModVersion=$version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Mod build failed" -ForegroundColor Red; exit 1
}

# ── 4. Build installer ──────────────────────────────────────────────────────

Write-Host "`nBuilding installer (Release)..." -ForegroundColor Cyan
$installerCsproj = Join-Path $root 'installer\AccessibleArenaInstaller\AccessibleArenaInstaller.csproj'
dotnet build $installerCsproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Installer build failed" -ForegroundColor Red; exit 1
}

# ── 5. Verify artifacts ─────────────────────────────────────────────────────

$modDll = Join-Path $root 'src\bin\Release\net472\AccessibleArena.dll'
$installerExe = Join-Path $root 'installer\AccessibleArenaInstaller\bin\Release\net472\AccessibleArenaInstaller.exe'

if (-not (Test-Path $modDll)) {
    Write-Host "ERROR: Mod DLL not found at $modDll" -ForegroundColor Red; exit 1
}
if (-not (Test-Path $installerExe)) {
    Write-Host "ERROR: Installer EXE not found at $installerExe" -ForegroundColor Red; exit 1
}

Write-Host "Artifacts verified" -ForegroundColor Green

# ── 6. Extract release notes ────────────────────────────────────────────────

$lines = Get-Content $changelogFile
$notes = @()
$capturing = $false

foreach ($line in $lines) {
    if ($line -match "^## $([regex]::Escape($tag))\s*$") {
        $capturing = $true
        continue
    }
    if ($capturing -and ($line -match '^## v' -or $line -match '^---')) {
        break
    }
    if ($capturing) {
        $notes += $line
    }
}

# Trim leading/trailing blank lines
while ($notes.Count -gt 0 -and $notes[0].Trim() -eq '') { $notes = $notes[1..($notes.Count-1)] }
while ($notes.Count -gt 0 -and $notes[-1].Trim() -eq '') { $notes = $notes[0..($notes.Count-2)] }

if ($notes.Count -eq 0) {
    Write-Host "WARNING: No release notes extracted (section empty)" -ForegroundColor Yellow
    $notesText = "Release $tag"
} else {
    $notesText = $notes -join "`n"
}

# Append SHA256 verification block
$installerHash = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash.ToLower()
$modHash       = (Get-FileHash -Path $modDll       -Algorithm SHA256).Hash.ToLower()

$notesText += @"


---

**Verification (SHA256):**

- ``AccessibleArenaInstaller.exe``: ``$installerHash``
- ``AccessibleArena.dll``: ``$modHash``

Verify in PowerShell with ``Get-FileHash <filename> -Algorithm SHA256`` or in Command Prompt with ``certutil -hashfile <filename> SHA256``.
"@

$notesFile = Join-Path $root 'release_notes.txt'
$notesText | Out-File -FilePath $notesFile -Encoding utf8

Write-Host "Release notes extracted ($($notes.Count) lines)" -ForegroundColor Green

# ── 7. Create git tag ────────────────────────────────────────────────────────

Write-Host "`nCreating tag $tag..." -ForegroundColor Cyan
git tag -a $tag -m "Release $tag"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to create tag" -ForegroundColor Red; exit 1
}

# ── 8. Push tag ──────────────────────────────────────────────────────────────

Write-Host "Pushing tag $tag..." -ForegroundColor Cyan
git push origin $tag
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to push tag. Deleting local tag." -ForegroundColor Red
    git tag -d $tag
    exit 1
}

# ── 9. Create GitHub release ─────────────────────────────────────────────────

Write-Host "`nCreating GitHub release..." -ForegroundColor Cyan
gh release create $tag `
    --title $tag `
    --notes-file $notesFile `
    $modDll `
    $installerExe

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to create GitHub release" -ForegroundColor Red; exit 1
}

# Cleanup
Remove-Item $notesFile -ErrorAction SilentlyContinue

Write-Host "`nRelease $tag published successfully!" -ForegroundColor Green
