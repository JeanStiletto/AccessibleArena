$assemblies = @(
    'C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\Assembly-CSharp.dll',
    'C:\Users\fabia\arena\libs\Core.dll'
)
$keywords = @('Download', 'Progress', 'Loading', 'Asset', 'Bundle', 'Patch', 'Update')

foreach ($assemblyPath in $assemblies) {
    if (Test-Path $assemblyPath) {
        Write-Host "`n=== Analyzing: $assemblyPath ===`n"
        try {
            $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
            $types = @()
            try {
                $types = $assembly.GetTypes()
            } catch [System.Reflection.ReflectionTypeLoadException] {
                $types = $_.Exception.Types | Where-Object { $_ -ne $null }
            }

            foreach ($type in $types) {
                $name = $type.FullName
                foreach ($kw in $keywords) {
                    if ($name -match $kw) {
                        Write-Host $name
                        break
                    }
                }
            }
        } catch {
            Write-Host "Error loading: $_"
        }
    }
}
