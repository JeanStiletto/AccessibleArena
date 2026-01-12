$assemblyPath = "C:\Users\fabia\arena\libs\Core.dll"
$outputPath = "C:\Users\fabia\arena\analysis_input.txt"

Write-Host "Analyzing: $assemblyPath for input/keybind handling"

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

    $keywords = @('Input', 'Key', 'Keyboard', 'Bind', 'Hotkey', 'Shortcut', 'Action', 'Command', 'Control', 'Handler', 'KeyCode', 'Press', 'Down', 'Up')

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
    $output += "MTGA Core.dll Analysis - Input/Keybind System"
    $output += "Generated: $(Get-Date)"
    $output += "Total matched: $($results.Count)"
    $output += "=" * 60
    $output += ""

    foreach ($type in ($results | Sort-Object FullName)) {
        $output += "CLASS: $($type.FullName)"
        if ($type.BaseType) {
            $output += "  Base: $($type.BaseType.Name)"
        }
        $output += "  IsInterface: $($type.IsInterface)"
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

        try {
            $fields = $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
            if ($fields.Count -gt 0) {
                $output += "  Static Fields:"
                foreach ($field in $fields) {
                    $output += "    $($field.FieldType.Name) $($field.Name)"
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
