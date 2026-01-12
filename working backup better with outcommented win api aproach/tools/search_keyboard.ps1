# Search for KeyboardManager and IAcceptActionHandler in game assemblies
$analyzer = "C:\Users\fabia\arena\tools\AssemblyAnalyzer\AssemblyAnalyzer.exe"
$managedPath = "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed"

Write-Host "=== Searching Core.dll for Accept/Keyboard ===" -ForegroundColor Green
& $analyzer "$managedPath\Core.dll" 2>&1 | Select-String -Pattern "Accept|Keyboard|IKey" | Select-Object -First 30

Write-Host "`n=== Searching Assembly-CSharp.dll ===" -ForegroundColor Green
& $analyzer "$managedPath\Assembly-CSharp.dll" 2>&1 | Select-String -Pattern "Accept|Keyboard|NPE" | Select-Object -First 30
