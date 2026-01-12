# Search for reward-related scene names in game logs
$logPath = "C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log"
if (Test-Path $logPath) {
    Get-Content $logPath | Select-String -Pattern "Scene|NPE|reward|chest|tutorial" | Select-Object -First 30
}

# List any scene-related files
Write-Host "`n=== Searching StreamingAssets ==="
Get-ChildItem "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\StreamingAssets" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "scene|npe|reward|tutorial" } |
    Select-Object -First 10 Name
