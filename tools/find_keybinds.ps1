# Find keybind strings in Core.dll
$bytes = [System.IO.File]::ReadAllBytes("C:\Users\fabia\arena\libs\Core.dll")
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
$strings = $text -split [char]0

Write-Host "Searching for keybind-related strings..."

# Find keybind UI text and localization keys
$keybindStrings = $strings | Where-Object {
    ($_ -match 'Keybind|Hotkey|Float.*All|Quick.*Tap|Full.*Control|Show.*Collection' -or
     $_ -match 'DuelScene.*Key|Settings.*Key|Gameplay.*Key' -or
     $_ -match '_floatAll|_quickTap|_fullControl') -and
    $_.Length -gt 3 -and $_.Length -lt 150
} | Sort-Object -Unique

Write-Host "`nKEYBIND UI STRINGS:"
$keybindStrings | ForEach-Object { Write-Host "  $_" }

# Find keyboard shortcut descriptions
Write-Host "`n`nKEYBOARD SHORTCUT DESCRIPTIONS:"
$shortcutStrings = $strings | Where-Object {
    $_ -match 'MainNav_Settings_Gameplay|DuelScene_.*Menu.*Key|DuelScene_EscapeMenu' -and
    $_.Length -gt 5 -and $_.Length -lt 100
} | Sort-Object -Unique

$shortcutStrings | ForEach-Object { Write-Host "  $_" }

# Find action names
Write-Host "`n`nACTION NAMES (from MTGAInput):"
$actionStrings = $strings | Where-Object {
    $_ -match '^(Escape|Next|Accept|Navigate|Submit|Cancel|Find|AltView|Toggle|Open|Close)$' -or
    $_ -match '^m_(UI|Debug|CustomInput)_'
} | Sort-Object -Unique

$actionStrings | ForEach-Object { Write-Host "  $_" }
