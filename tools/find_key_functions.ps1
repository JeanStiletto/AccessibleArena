# Find what each key does
$bytes = [System.IO.File]::ReadAllBytes("C:\Users\fabia\arena\libs\Core.dll")
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
$strings = $text -split [char]0

Write-Host "=== NATIVE MTGA KEYBINDS ==="
Write-Host ""

# Search for keybind function associations
Write-Host "GAMEPLAY KEYS (from settings localization):"
$keyStrings = $strings | Where-Object { $_ -match 'MainNav_Settings_Gameplay_Key_' }
$keyStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "DUEL SCENE HOTKEYS:"
$hotkeyStrings = $strings | Where-Object { $_ -match 'DuelScene_SettingsMenu_Gameplay_' -or $_ -match 'DuelScene.*HotKey' }
$hotkeyStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "FULL CONTROL RELATED:"
$fullControlStrings = $strings | Where-Object { $_ -match 'FullControl' -and $_.Length -lt 80 }
$fullControlStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "CLIENT PROMPTS (button actions):"
$promptStrings = $strings | Where-Object { $_ -match 'DuelScene_ClientPrompt.*Button' -or $_ -match 'ClientPrompt.*Key' }
$promptStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "UNDO/REDO:"
$undoStrings = $strings | Where-Object { $_ -match 'Undo|Redo' -and $_.Length -lt 60 }
$undoStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "PASS/RESOLVE:"
$passStrings = $strings | Where-Object { ($_ -match 'Pass|Resolve|Skip|Done|Confirm' -and $_ -match 'Button|Key|Action') -and $_.Length -lt 80 }
$passStrings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
