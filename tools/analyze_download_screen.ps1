$assemblyPath = 'C:\Users\fabia\arena\libs\Core.dll'
$targetTypes = @('AssetPrepScene', 'AssetPrepScreen', 'BackgroundDownloadingGUI', 'NavContentLoadingView', 'ProgressBarController', 'LoadingPanelShowing')

Write-Host "Analyzing download-related types..."

try {
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
    $types = @()
    try {
        $types = $assembly.GetTypes()
    } catch [System.Reflection.ReflectionTypeLoadException] {
        $types = $_.Exception.Types | Where-Object { $_ -ne $null }
    }

    foreach ($type in $types) {
        $name = $type.Name
        if ($targetTypes -contains $name) {
            Write-Host "`n=========================================="
            Write-Host "CLASS: $($type.FullName)"
            Write-Host "=========================================="
            if ($type.BaseType) {
                Write-Host "  Base: $($type.BaseType.FullName)"
            }
            Write-Host "  IsInterface: $($type.IsInterface)"
            Write-Host "  IsAbstract: $($type.IsAbstract)"

            try {
                $fields = $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static)
                if ($fields.Count -gt 0) {
                    Write-Host "`n  Fields:"
                    foreach ($field in $fields) {
                        Write-Host "    $($field.FieldType.Name) $($field.Name)"
                    }
                }
            } catch {}

            try {
                $props = $type.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static)
                if ($props.Count -gt 0) {
                    Write-Host "`n  Properties:"
                    foreach ($prop in $props) {
                        Write-Host "    $($prop.PropertyType.Name) $($prop.Name)"
                    }
                }
            } catch {}

            try {
                $methods = $type.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly) | Where-Object { -not $_.IsSpecialName }
                if ($methods.Count -gt 0) {
                    Write-Host "`n  Methods:"
                    foreach ($method in $methods) {
                        $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                        Write-Host "    $($method.ReturnType.Name) $($method.Name)($params)"
                    }
                }
            } catch {}
        }
    }
} catch {
    Write-Host "Error: $_"
}
