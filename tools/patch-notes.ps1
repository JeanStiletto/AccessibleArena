# Fetch MTG Arena patch notes as plain text.
#
# WotC publishes no developer changelog, but the support-site patch notes do carry a
# "Bug Fixes"/"Known Issues" section that occasionally explains an observed behaviour change.
# Treat it as context, never as the regression check - the UI internals the mod binds to are
# never mentioned there. Use tools\check-game-update.ps1 for the actual breakage list.
#
# Usage:
#   powershell -NoProfile -File tools\patch-notes.ps1            # list the 10 newest releases
#   powershell -NoProfile -File tools\patch-notes.ps1 -Latest     # print the newest one in full
#   powershell -NoProfile -File tools\patch-notes.ps1 -Version 2026.62.10

param(
    [switch]$Latest,
    [string]$Version = "",
    [int]$Count = 10
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$sectionId = 4402585813268   # help centre section "Patch Notes"
$api = "https://mtgarena-support.wizards.com/api/v2/help_center/en-us/sections/$sectionId/articles.json"

try {
    $response = Invoke-RestMethod -Uri "$api`?sort_by=created_at&sort_order=desc&per_page=30" -UseBasicParsing
} catch {
    Write-Host "Could not reach the patch notes API: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$articles = @($response.articles)

function Show-Article($article) {
    Write-Host ""
    Write-Host $article.title -ForegroundColor Cyan
    Write-Host ("published " + ([datetime]$article.created_at).ToString("yyyy-MM-dd"))
    Write-Host $article.html_url
    Write-Host ""

    # Strip markup, keep block structure so headings and list items land on their own lines.
    $text = $article.body
    $text = [regex]::Replace($text, '(?is)<(script|style).*?</\1>', '')
    $text = [regex]::Replace($text, '(?i)<li[^>]*>', "`n  - ")
    $text = [regex]::Replace($text, '(?i)<(br|/p|/h[1-6]|/div|/tr)[^>]*>', "`n")
    $text = [regex]::Replace($text, '<[^>]+>', '')
    $text = [Net.WebUtility]::HtmlDecode($text)
    $text = [regex]::Replace($text, '[ \t]+', ' ')
    $text = [regex]::Replace($text, '(\r?\n\s*){3,}', "`n`n")
    Write-Host $text.Trim()
}

if ($Version) {
    $match = $articles | Where-Object { $_.title -like "*$Version*" } | Select-Object -First 1
    if (-not $match) {
        Write-Host "No patch notes found for '$Version'. Newest available: $($articles[0].title)" -ForegroundColor Yellow
        exit 1
    }
    Show-Article $match
    exit 0
}

if ($Latest) {
    Show-Article $articles[0]
    exit 0
}

Write-Host ""
Write-Host "Newest MTG Arena patch notes:" -ForegroundColor Cyan
foreach ($article in ($articles | Select-Object -First $Count)) {
    Write-Host ("  " + ([datetime]$article.created_at).ToString("yyyy-MM-dd") + "  " + $article.title)
}
Write-Host ""
Write-Host "Print one with:  tools\patch-notes.ps1 -Version <x.y.z>"
