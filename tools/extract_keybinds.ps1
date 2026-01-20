# Extract keybind-related information from Core.dll
$dllPath = "C:\Users\fabia\arena\libs\Core.dll"
$outputPath = "C:\Users\fabia\arena\analysis_keybinds.txt"

Write-Host "Analyzing $dllPath for keybindings..."

# Load assembly and extract types with detailed analysis
try {
    $assembly = [System.Reflection.Assembly]::LoadFrom($dllPath)
    $types = @()

    try {
        $types = $assembly.GetTypes()
    }
    catch [System.Reflection.ReflectionTypeLoadException] {
        $types = $_.Exception.Types | Where-Object { $_ -ne $null }
    }

    $output = @()
    $output += "MTGA Keybind Analysis"
    $output += "Generated: $(Get-Date)"
    $output += "=" * 60

    # Find KeyboardManager
    $keyboardManager = $types | Where-Object { $_.Name -eq 'KeyboardManager' } | Select-Object -First 1
    if ($keyboardManager) {
        $output += "`nKEYBOARD MANAGER: $($keyboardManager.FullName)"
        $output += "Base: $($keyboardManager.BaseType.Name)"

        # Get all members including private
        $allFlags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

        $output += "`nFields:"
        foreach ($field in $keyboardManager.GetFields($allFlags)) {
            $output += "  $($field.FieldType.Name) $($field.Name)"
        }

        $output += "`nMethods:"
        foreach ($method in $keyboardManager.GetMethods($allFlags)) {
            if (-not $method.IsSpecialName) {
                $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                $output += "  $($method.ReturnType.Name) $($method.Name)($params)"
            }
        }
    }

    # Find MTGAInput
    $mtgaInput = $types | Where-Object { $_.Name -eq 'MTGAInput' -or $_.FullName -match 'MTGAInput' } | Select-Object -First 1
    if ($mtgaInput) {
        $output += "`n" + "=" * 60
        $output += "MTGA INPUT: $($mtgaInput.FullName)"
        $output += "Base: $($mtgaInput.BaseType.Name)"

        $allFlags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

        $output += "`nFields:"
        foreach ($field in $mtgaInput.GetFields($allFlags)) {
            $output += "  $($field.FieldType.Name) $($field.Name)"
        }

        $output += "`nProperties:"
        foreach ($prop in $mtgaInput.GetProperties($allFlags)) {
            $output += "  $($prop.PropertyType.Name) $($prop.Name)"
        }

        $output += "`nMethods:"
        foreach ($method in $mtgaInput.GetMethods($allFlags)) {
            if (-not $method.IsSpecialName) {
                $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                $output += "  $($method.ReturnType.Name) $($method.Name)($params)"
            }
        }

        # Get nested types (like CustomInputActions)
        $output += "`nNested Types:"
        foreach ($nested in $mtgaInput.GetNestedTypes($allFlags)) {
            $output += "`n  NESTED: $($nested.Name)"
            $output += "    Base: $($nested.BaseType.Name)"
            foreach ($field in $nested.GetFields($allFlags)) {
                $output += "    Field: $($field.FieldType.Name) $($field.Name)"
            }
            foreach ($prop in $nested.GetProperties($allFlags)) {
                $output += "    Property: $($prop.PropertyType.Name) $($prop.Name)"
            }
        }
    }

    # Find SettingsPanelHotkeys
    $hotkeyPanel = $types | Where-Object { $_.Name -eq 'SettingsPanelHotkeys' } | Select-Object -First 1
    if ($hotkeyPanel) {
        $output += "`n" + "=" * 60
        $output += "SETTINGS PANEL HOTKEYS: $($hotkeyPanel.FullName)"

        $allFlags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

        $output += "`nFields:"
        foreach ($field in $hotkeyPanel.GetFields($allFlags)) {
            $output += "  $($field.FieldType.Name) $($field.Name)"
        }

        $output += "`nMethods:"
        foreach ($method in $hotkeyPanel.GetMethods($allFlags)) {
            if (-not $method.IsSpecialName) {
                $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                $output += "  $($method.ReturnType.Name) $($method.Name)($params)"
            }
        }
    }

    # Find IKeybindingWorkflow
    $keybindWorkflow = $types | Where-Object { $_.Name -eq 'IKeybindingWorkflow' } | Select-Object -First 1
    if ($keybindWorkflow) {
        $output += "`n" + "=" * 60
        $output += "KEYBINDING WORKFLOW: $($keybindWorkflow.FullName)"

        $output += "`nMethods:"
        foreach ($method in $keybindWorkflow.GetMethods()) {
            $params = ($method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            $output += "  $($method.ReturnType.Name) $($method.Name)($params)"
        }
    }

    # Find all classes implementing IKeybindingWorkflow
    $output += "`n" + "=" * 60
    $output += "CLASSES IMPLEMENTING IKeybindingWorkflow:"

    foreach ($type in $types) {
        try {
            if ($type.GetInterfaces() | Where-Object { $_.Name -eq 'IKeybindingWorkflow' }) {
                $output += "  $($type.FullName)"
            }
        } catch {}
    }

    # Look for keybind config or settings
    $output += "`n" + "=" * 60
    $output += "KEYBIND-RELATED TYPES:"

    $keybindTypes = $types | Where-Object {
        $_.Name -match 'Keybind|Hotkey|Shortcut|InputAction' -and
        $_.Name -notmatch 'Extensions$'
    }

    foreach ($type in $keybindTypes) {
        $output += "`n  TYPE: $($type.FullName)"
        $output += "    Base: $($type.BaseType.Name)"
        $output += "    IsInterface: $($type.IsInterface)"

        $allFlags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

        foreach ($field in $type.GetFields($allFlags)) {
            $output += "    Field: $($field.FieldType.Name) $($field.Name)"
        }
    }

    $output | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "Results written to: $outputPath"
    Write-Host "Found $(($output | Measure-Object).Count) lines"
}
catch {
    Write-Host "Error: $_"
}
