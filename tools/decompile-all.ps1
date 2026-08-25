# Batch-decompile all types listed in llm-docs/type-index.md
# Run after game updates to refresh the pre-decompiled reference files.
# Usage: powershell -NoProfile -File tools\decompile-all.ps1

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$indexFile = Join-Path $repoRoot "llm-docs\type-index.md"
$decompileScript = Join-Path $repoRoot "tools\decompile.ps1"

if (-not (Test-Path $indexFile)) {
    Write-Host "Type index not found at $indexFile" -ForegroundColor Red
    exit 1
}

# Parse type-index.md: lines like "| TypeName | full.namespace.TypeName | Core |"
$lines = Get-Content $indexFile
$total = 0
$succeeded = 0
$failed = 0
$skipped = 0

foreach ($line in $lines) {
    # Match table rows: | ShortName | FullNamespace | DLL |
    if ($line -match '^\|\s*`?([^`|]+)`?\s*\|\s*`?([^`|]+)`?\s*\|\s*`?([^`|]+)`?\s*\|') {
        $shortName = $matches[1].Trim()
        $fullName = $matches[2].Trim()
        $dll = $matches[3].Trim()

        # Skip header rows
        if ($shortName -eq "Short Name" -or $shortName -match "^-+$") { continue }

        # Skip note-rows: a full-name column starting with "(" documents a type that
        # is runtime-only, removed from the game, or covered by another row's file.
        if ($fullName.StartsWith("(")) { $skipped++; continue }

        $total++
        Write-Host "`n[$total] Decompiling $shortName ($dll)..." -ForegroundColor White

        try {
            & $decompileScript -TypeName $fullName -Dll $dll
            if ($LASTEXITCODE -eq 0) {
                $succeeded++
            } else {
                $failed++
            }
        } catch {
            Write-Host "  Error: $_" -ForegroundColor Red
            $failed++
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Decompile complete: $succeeded/$total succeeded, $failed failed, $skipped note-rows skipped" -ForegroundColor Cyan
