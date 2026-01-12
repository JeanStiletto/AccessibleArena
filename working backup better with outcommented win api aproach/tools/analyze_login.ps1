$assemblyPath = "C:\Users\fabia\arena\libs\Assembly-CSharp.dll"
$outputPath = "C:\Users\fabia\arena\analysis_login.txt"

Write-Host "Analyzing: $assemblyPath"

try {
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
    $types = @()

    try {
        $types = $assembly.GetTypes()
    }
    catch [System.Reflection.ReflectionTypeLoadException] {
        Write-Host "Partial load - processing available types"
        $types = $_.Exception.Types | Where-Object { $_ -ne $null }
    }

    Write-Host "Total types found: $($types.Count)"

    $keywords = @('Login', 'Auth', 'Account', 'Register', 'Password', 'Email', 'Age', 'Verif', 'Welcome', 'Splash', 'Menu', 'Dialog', 'Modal', 'Scene', 'Loading', 'Screen', 'Panel', 'Navigation', 'Button', 'Form', 'Popup', 'Overlay')

    $results = @()

    foreach ($type in $types) {
        $name = $type.FullName
        foreach ($kw in $keywords) {
            if ($name -match $kw) {
                $results += $type
                break
            }
        }
    }

    Write-Host "Matched types: $($results.Count)"

    $output = @()
    $output += "MTGA Assembly-CSharp.dll Analysis - Login/Auth/Menu Related Classes"
    $output += "Generated: $(Get-Date)"
    $output += "Total matched: $($results.Count)"
    $output += "=" * 60
    $output += ""

    foreach ($type in ($results | Sort-Object FullName)) {
        $output += "CLASS: $($type.FullName)"
        $output += "  Base: $($type.BaseType.Name)"
        $output += "  Public: $($type.IsPublic)"

        try {
            $props = $type.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static)
            if ($props.Count -gt 0) {
                $output += "  Properties:"
                foreach ($prop in $props) {
                    $output += "    $($prop.PropertyType.Name) $($prop.Name)"
                }
            }
        } catch {}

        try {
            $methods = $type.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Where-Object { -not $_.IsSpecialName }
            if ($methods.Count -gt 0) {
                $output += "  Methods:"
                foreach ($method in $methods) {
                    $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                    $output += "    $($method.ReturnType.Name) $($method.Name)($params)"
                }
            }
        } catch {}

        $output += "-" * 40
        $output += ""
    }

    $output | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "Results written to: $outputPath"
}
catch {
    Write-Host "Error: $_"
}
