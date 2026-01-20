$file = 'C:\Users\fabia\arena\src\Core\Services\CardDetector.cs'
$content = [System.IO.File]::ReadAllText($file)
$content = $content -replace 'new CardInfoBlock\("Name"', 'new CardInfoBlock(Models.Strings.CardInfoName'
$content = $content -replace 'new CardInfoBlock\("Mana Cost"', 'new CardInfoBlock(Models.Strings.CardInfoManaCost'
$content = $content -replace 'new CardInfoBlock\("Power and Toughness"', 'new CardInfoBlock(Models.Strings.CardInfoPowerToughness'
$content = $content -replace 'new CardInfoBlock\("Type"', 'new CardInfoBlock(Models.Strings.CardInfoType'
$content = $content -replace 'new CardInfoBlock\("Rules"', 'new CardInfoBlock(Models.Strings.CardInfoRules'
$content = $content -replace 'new CardInfoBlock\("Flavor"', 'new CardInfoBlock(Models.Strings.CardInfoFlavor'
$content = $content -replace 'new CardInfoBlock\("Artist"', 'new CardInfoBlock(Models.Strings.CardInfoArtist'
[System.IO.File]::WriteAllText($file, $content)
Write-Host "Done"
