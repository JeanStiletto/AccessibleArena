# Regression check for MTGA game updates.
#
# The mod binds to the game in two ways:
#   1. compile-time references (Core.dll, Assembly-CSharp.dll, ...) -> `dotnet build` catches breakage
#   2. reflection by string name -> nothing catches breakage until a user reports it
# This script covers (2), plus a signature-level diff of the game types the mod tracks.
#
# Usage:
#   powershell -NoProfile -File tools\check-game-update.ps1
#       Report regressions against the stored baseline.
#   powershell -NoProfile -File tools\check-game-update.ps1 -FromDecompiled
#       No baseline yet (or it is stale)? Use llm-docs/decompiled/*.cs written before the
#       update as the reference instead. Type-scoped, so the most precise mode available
#       right after an update.
#   powershell -NoProfile -File tools\check-game-update.ps1 -UpdateBaseline
#       Record the current game as "known good". Run this once the mod works again.
#
# Exit code 0 = no new breakage, 1 = regressions found, 2 = setup problem.

param(
    [switch]$UpdateBaseline,
    [switch]$FromDecompiled,
    [string]$ManagedDir = "",
    # Scan a different copy of the mod sources - e.g. an export of an older commit, to ask
    # "would this check have caught that regression?". Never combine with -UpdateBaseline.
    [string]$SourceRoot = "",
    [int]$MaxDetail = 12
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

# --- Game paths -------------------------------------------------------------

if (-not $ManagedDir) {
    $localProps = Join-Path $repoRoot "src\local.props"
    if (Test-Path $localProps) {
        $node = ([xml](Get-Content $localProps)).SelectSingleNode("//MtgaPath")
        if ($node -and $node.InnerText) { $ManagedDir = Join-Path $node.InnerText "MTGA_Data\Managed" }
    }
}
if (-not $ManagedDir -or -not (Test-Path $ManagedDir)) {
    foreach ($candidate in @(
        "C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed",
        "C:\Program Files (x86)\Steam\steamapps\common\MTGA\MTGA_Data\Managed")) {
        if (Test-Path $candidate) { $ManagedDir = $candidate; break }
    }
}
if (-not $ManagedDir -or -not (Test-Path $ManagedDir)) {
    Write-Host "MTGA managed directory not found. Pass -ManagedDir or create src/local.props." -ForegroundColor Red
    exit 2
}
$gameRoot = Split-Path -Parent (Split-Path -Parent $ManagedDir)

# Mono.Cecil ships with MelonLoader; it reads assemblies without loading their dependencies.
$cecilPath = Join-Path $gameRoot "MelonLoader\net35\Mono.Cecil.dll"
if (-not (Test-Path $cecilPath)) {
    Write-Host "Mono.Cecil not found at $cecilPath (MelonLoader missing?)." -ForegroundColor Red
    exit 2
}

# Assemblies the mod reflects into. Unity ones are included so that TMP/UI member names
# do not show up as false "missing" hits.
$dllNames = @(
    "Core.dll", "Assembly-CSharp.dll", "SharedClientCore.dll", "Wizards.MDN.GreProtobuf.dll",
    "Wizards.Arena.Models.dll", "Wizards.Arena.Enums.dll", "Wizards.Mtga.Metadata.dll",
    "Wizards.Mtga.Interfaces.dll", "ZFBrowser.dll",
    "Unity.TextMeshPro.dll", "Unity.InputSystem.dll"
)
$dllPaths = @()
foreach ($n in $dllNames) {
    $p = Join-Path $ManagedDir $n
    if (Test-Path $p) { $dllPaths += $p }
}
foreach ($unity in (Get-ChildItem $ManagedDir -Filter "UnityEngine*.dll")) { $dllPaths += $unity.FullName }

function Get-GameVersion {
    $log = Join-Path $gameRoot "MelonLoader\Latest.log"
    if (Test-Path $log) {
        $line = Select-String -Path $log -Pattern "Game Version:\s*(\S+)" -List
        if ($line) { return $line.Matches[0].Groups[1].Value }
    }
    return "unknown (Core.dll " + (Get-Item (Join-Path $ManagedDir "Core.dll")).LastWriteTime.ToString("yyyy-MM-dd") + ")"
}

# --- Cecil-backed index -----------------------------------------------------

$cecil = [System.Reflection.Assembly]::LoadFrom($cecilPath)
$onResolve = [System.ResolveEventHandler] {
    param($sender, $e)
    if ($e.Name -like "Mono.Cecil,*") { return $cecil }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($onResolve)

$helperSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Cecil;

public static class GameApi
{
    static List<ModuleDefinition> _modules = new List<ModuleDefinition>();
    static Dictionary<string, List<TypeDefinition>> _byShort = new Dictionary<string, List<TypeDefinition>>(StringComparer.Ordinal);
    static Dictionary<string, TypeDefinition> _byFull = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
    static HashSet<string> _members = new HashSet<string>(StringComparer.Ordinal);

    public static void Load(string[] dllPaths)
    {
        foreach (string path in dllPaths)
        {
            if (!File.Exists(path)) continue;
            ModuleDefinition module = ModuleDefinition.ReadModule(path, new ReaderParameters(ReadingMode.Deferred));
            _modules.Add(module);
            foreach (TypeDefinition t in module.GetTypes())
            {
                if (IsGenerated(t.Name)) continue;
                List<TypeDefinition> bucket;
                if (!_byShort.TryGetValue(t.Name, out bucket))
                {
                    bucket = new List<TypeDefinition>();
                    _byShort[t.Name] = bucket;
                }
                bucket.Add(t);
                string full = t.FullName.Replace('/', '.');
                if (!_byFull.ContainsKey(full)) _byFull[full] = t;
                foreach (FieldDefinition f in t.Fields) if (!IsGenerated(f.Name)) _members.Add(f.Name);
                foreach (PropertyDefinition p in t.Properties) if (!IsGenerated(p.Name)) _members.Add(p.Name);
                foreach (MethodDefinition m in t.Methods) if (!IsGenerated(m.Name)) _members.Add(m.Name);
                foreach (EventDefinition e in t.Events) if (!IsGenerated(e.Name)) _members.Add(e.Name);
            }
        }
    }

    static bool IsGenerated(string name)
    {
        return name.IndexOf('<') >= 0 || name.IndexOf('$') >= 0;
    }

    public static bool HasMember(string name) { return _members.Contains(name); }

    public static string[] OwnersOf(string member, int max)
    {
        List<string> owners = new List<string>();
        foreach (ModuleDefinition module in _modules)
        {
            foreach (TypeDefinition t in module.GetTypes())
            {
                if (Declares(t, member)) { owners.Add(t.FullName); if (owners.Count >= max) return owners.ToArray(); }
            }
        }
        return owners.ToArray();
    }

    static bool Declares(TypeDefinition t, string member)
    {
        foreach (FieldDefinition f in t.Fields) if (f.Name == member) return true;
        foreach (PropertyDefinition p in t.Properties) if (p.Name == member) return true;
        foreach (MethodDefinition m in t.Methods) if (m.Name == member) return true;
        foreach (EventDefinition e in t.Events) if (e.Name == member) return true;
        return false;
    }

    public static TypeDefinition Find(string name)
    {
        string key = name.Replace('+', '.').Replace('/', '.');
        TypeDefinition t;
        if (_byFull.TryGetValue(key, out t)) return t;
        int dot = key.LastIndexOf('.');
        string shortName = dot >= 0 ? key.Substring(dot + 1) : key;
        List<TypeDefinition> bucket;
        if (_byShort.TryGetValue(shortName, out bucket))
        {
            // A bare short name matches; a namespaced one only counts if the namespace agrees.
            if (dot < 0)
            {
                // Short names collide (Wotc.Mtga.Login.Panel vs a nested SocialReportPlayer.Panel).
                // A nested helper is never what the mod means by a bare name.
                foreach (TypeDefinition candidate in bucket)
                    if (!candidate.IsNested) return candidate;
                return bucket[0];
            }
            foreach (TypeDefinition candidate in bucket)
                if (candidate.FullName.Replace('/', '.').EndsWith(key, StringComparison.Ordinal)) return candidate;
        }
        return null;
    }

    public static bool HasType(string name) { return Find(name) != null; }

    // Nested types count as part of the outer type: a decompiled file shows
    // MDNPlayerPrefs.Strings.Foo inline, so its constants must not read as missing.
    static void Collect(TypeDefinition t, HashSet<string> names)
    {
        foreach (FieldDefinition f in t.Fields) names.Add(f.Name);
        foreach (PropertyDefinition p in t.Properties) names.Add(p.Name);
        foreach (MethodDefinition m in t.Methods) names.Add(m.Name);
        foreach (EventDefinition e in t.Events) names.Add(e.Name);
        foreach (TypeDefinition nested in t.NestedTypes)
        {
            names.Add(nested.Name);
            Collect(nested, names);
        }
    }

    public static string[] MembersOfType(string name)
    {
        TypeDefinition t = Find(name);
        if (t == null) return null;
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        while (t != null)
        {
            Collect(t, names);
            TypeReference b = t.BaseType;
            t = (b == null) ? null : Find(b.FullName.Replace('/', '.'));
        }
        List<string> result = new List<string>(names);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    // One block per type: "## FullName [module] : BaseType" followed by sorted member lines.
    public static string[] Snapshot(string[] typeNames)
    {
        List<string> lines = new List<string>();
        List<string> wanted = new List<string>(typeNames);
        wanted.Sort(StringComparer.Ordinal);
        HashSet<string> emitted = new HashSet<string>(StringComparer.Ordinal);

        foreach (string name in wanted)
        {
            TypeDefinition t = Find(name);
            if (t == null) continue;
            if (!emitted.Add(t.FullName)) continue;

            string baseName = t.BaseType == null ? "-" : t.BaseType.Name;
            lines.Add("## " + t.FullName + " [" + t.Module.Name + "] : " + baseName);

            List<string> members = new List<string>();
            if (t.IsEnum)
            {
                foreach (FieldDefinition f in t.Fields)
                {
                    if (!f.HasConstant) continue;
                    members.Add("value " + f.Name + " = " + Convert.ToString(f.Constant));
                }
            }
            else
            {
                foreach (FieldDefinition f in t.Fields)
                {
                    if (IsGenerated(f.Name)) continue;
                    members.Add("field " + f.Name + " : " + f.FieldType.Name);
                }
                foreach (PropertyDefinition p in t.Properties)
                {
                    if (IsGenerated(p.Name)) continue;
                    members.Add("prop " + p.Name + " : " + p.PropertyType.Name);
                }
                foreach (MethodDefinition m in t.Methods)
                {
                    if (IsGenerated(m.Name)) continue;
                    if (m.IsGetter || m.IsSetter || m.IsAddOn || m.IsRemoveOn) continue;
                    StringBuilder sb = new StringBuilder();
                    sb.Append("method ").Append(m.Name).Append('(');
                    for (int i = 0; i < m.Parameters.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(m.Parameters[i].ParameterType.Name);
                    }
                    sb.Append(") : ").Append(m.ReturnType.Name);
                    members.Add(sb.ToString());
                }
                foreach (EventDefinition e in t.Events)
                {
                    if (IsGenerated(e.Name)) continue;
                    members.Add("event " + e.Name + " : " + e.EventType.Name);
                }
            }
            members.Sort(StringComparer.Ordinal);
            lines.AddRange(members);
            lines.Add("");
        }
        return lines.ToArray();
    }
}
'@

Add-Type -TypeDefinition $helperSource -ReferencedAssemblies $cecilPath -Language CSharp
[GameApi]::Load($dllPaths)

# --- Collect what the mod asks the game for --------------------------------

$srcRoot = if ($SourceRoot) { $SourceRoot } else { Join-Path $repoRoot "src" }
if (-not (Test-Path $srcRoot)) {
    Write-Host "Source root not found: $srcRoot" -ForegroundColor Red
    exit 2
}
if ($SourceRoot -and $UpdateBaseline) {
    Write-Host "-SourceRoot cannot be combined with -UpdateBaseline." -ForegroundColor Red
    exit 2
}
$sourceFiles = Get-ChildItem $srcRoot -Recurse -Filter *.cs

$memberPatterns = @(
    '\.Get(?:Field|Property|Method|Member|Event)\(\s*"([A-Za-z_][A-Za-z0-9_]*)"',
    'AccessTools\.(?:Declared)?(?:Field|Property|Method|PropertyGetter|PropertySetter)\([^,)]*,\s*"([A-Za-z_][A-Za-z0-9_]*)"',
    'Get(?:Field|Property)Value\([^,]+,\s*"([A-Za-z_][A-Za-z0-9_]*)"',
    'HarmonyPatch\([^,)]+,\s*"([A-Za-z_][A-Za-z0-9_]*)"'
)
$typePatterns = @(
    'FindType\(\s*"([A-Za-z_][A-Za-z0-9_.+]*)"',
    'AccessTools\.TypeByName\(\s*"([A-Za-z_][A-Za-z0-9_.+]*)"',
    'GetComponent(?:InChildren|InParent)?\(\s*"([A-Za-z_][A-Za-z0-9_.]*)"',
    'HarmonyPatch\(\s*"([A-Za-z_][A-Za-z0-9_.]+)"'
)

# name -> list of "file:line"
$memberRefs = @{}
$typeRefs = @{}
# Names the mod only ever asks for as a `??` fallback. Those are written in the expectation
# that they may not resolve - an older client's spelling kept alongside the current one - so
# they are not evidence of breakage.
$fallbackOnly = @{}

function Add-Ref($table, $name, $where, $isFallback) {
    # Names built by concatenation ("Namespace." + x) leak a trailing dot into the capture.
    if (-not $name -or $name.EndsWith(".")) { return }
    if (-not $table.ContainsKey($name)) { $table[$name] = New-Object System.Collections.ArrayList }
    [void]$table[$name].Add($where)

    if (-not $isFallback) { $fallbackOnly[$name] = $false }
    elseif (-not $fallbackOnly.ContainsKey($name)) { $fallbackOnly[$name] = $true }
}

function Test-FallbackOnly($name) {
    return ($fallbackOnly.ContainsKey($name) -and $fallbackOnly[$name])
}

# $table.Count would hit a literal "Count" key before the property.
function Get-RefCount($table) { return $table.Keys.Count }

function Get-Sites($kind, $name) {
    # The leading comma keeps a single-element list from being unrolled by `return`.
    if ($kind -eq "member") { return ,@($memberRefs[$name]) }
    return ,@($typeRefs[$name])
}

$relBase = (Split-Path -Parent $srcRoot)
foreach ($file in $sourceFiles) {
    $rel = $file.FullName.Substring($relBase.Length + 1)
    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadAllLines($file.FullName)) {
        $lineNo++
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith("//") -or $trimmed.StartsWith("*")) { continue }
        $isFallback = $line.Contains("??")
        foreach ($pattern in $memberPatterns) {
            foreach ($m in [regex]::Matches($line, $pattern)) {
                Add-Ref $memberRefs $m.Groups[1].Value ($rel + ":" + $lineNo) $isFallback
            }
        }
        foreach ($pattern in $typePatterns) {
            foreach ($m in [regex]::Matches($line, $pattern)) {
                Add-Ref $typeRefs $m.Groups[1].Value ($rel + ":" + $lineNo) $isFallback
            }
        }
    }
}

# GameTypeNames constants are type names resolved indirectly, so pick them up from the constant file.
$typeNamesFile = Join-Path $srcRoot "Core\Constants\GameTypeNames.cs"
if (Test-Path $typeNamesFile) {
    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadAllLines($typeNamesFile)) {
        $lineNo++
        $m = [regex]::Match($line, 'const\s+string\s+\w+\s*=\s*"([^"]+)"')
        if ($m.Success) { Add-Ref $typeRefs $m.Groups[1].Value ("src\Core\Constants\GameTypeNames.cs:" + $lineNo) }
    }
}

# --- Which types get a signature snapshot -----------------------------------

$trackedTypes = New-Object System.Collections.Generic.HashSet[string]
foreach ($name in $typeRefs.Keys) { [void]$trackedTypes.Add($name) }

$indexFile = Join-Path $repoRoot "llm-docs\type-index.md"
if (Test-Path $indexFile) {
    foreach ($line in [System.IO.File]::ReadAllLines($indexFile)) {
        $m = [regex]::Match($line, '^\|\s*`?([^`|]+?)`?\s*\|\s*`?([^`|]+?)`?\s*\|')
        if ($m.Success) {
            $full = $m.Groups[2].Value.Trim()
            if ($full -and $full -ne "Full Namespace" -and $full -notmatch '^-+$') { [void]$trackedTypes.Add($full) }
        }
    }
}

# Enums the mod mirrors as raw numbers (src/Core/Constants/CardHolderTypes.cs and friends).
# A silent renumber here misroutes zone navigation, so they are always snapshotted.
foreach ($pinned in @(
    "CardHolderType", "ZoneTransferReason", "StopType", "Phase", "Step",
    "CardHighlightType", "DuelSceneMode")) {
    [void]$trackedTypes.Add($pinned)
}

# --- Baseline storage -------------------------------------------------------

$baselineDir = Join-Path $repoRoot "llm-docs\api-baseline"
$snapshotFile = Join-Path $baselineDir "api-snapshot.txt"
$namesFile = Join-Path $baselineDir "reflection-names.txt"
$metaFile = Join-Path $baselineDir "meta.txt"

$currentVersion = Get-GameVersion

# Current resolution status of every name the mod looks up.
$nameStatus = New-Object 'System.Collections.Generic.SortedDictionary[string,string]'
foreach ($name in $memberRefs.Keys) {
    $ok = [GameApi]::HasMember($name)
    $nameStatus["member " + $name] = $(if ($ok) { "ok" } else { "missing" })
}
foreach ($name in $typeRefs.Keys) {
    $ok = [GameApi]::HasType($name)
    $nameStatus["type " + $name] = $(if ($ok) { "ok" } else { "missing" })
}

if ($UpdateBaseline) {
    if (-not (Test-Path $baselineDir)) { New-Item -ItemType Directory -Path $baselineDir -Force | Out-Null }
    $lines = @()
    foreach ($key in $nameStatus.Keys) { $lines += ($nameStatus[$key] + " " + $key) }
    Set-Content -Path $namesFile -Value $lines -Encoding utf8

    $snapshot = [GameApi]::Snapshot([string[]]@($trackedTypes))
    Set-Content -Path $snapshotFile -Value $snapshot -Encoding utf8

    Set-Content -Path $metaFile -Encoding utf8 -Value @(
        "game version: $currentVersion",
        "recorded:     " + (Get-Date -Format "yyyy-MM-dd HH:mm"),
        "tracked types: " + $trackedTypes.Count,
        "reflection names: " + $nameStatus.Count
    )

    Write-Host ""
    Write-Host "Baseline recorded for game version $currentVersion" -ForegroundColor Green
    Write-Host "  $namesFile"
    Write-Host "  $snapshotFile"
    Write-Host "  $($nameStatus.Count) reflection names, $($trackedTypes.Count) tracked types"
    exit 0
}

# --- Report -----------------------------------------------------------------

Write-Host ""
Write-Host "Game update regression check" -ForegroundColor Cyan
Write-Host "  game version: $currentVersion"
Write-Host "  assemblies:   $($dllPaths.Count) from $ManagedDir"
Write-Host "  mod lookups:  $(Get-RefCount $memberRefs) member names, $(Get-RefCount $typeRefs) type names"

$regressions = 0

# The mod logs every reflection handle it fails to resolve. That covers types only reachable
# at runtime, which no static scan can see, so read the last session's log first.
$latestLog = Join-Path $gameRoot "MelonLoader\Latest.log"
Write-Host ""
if (Test-Path $latestLog) {
    # Narrow on purpose. A bare "Could not find" also matches routine misses like the Colour
    # Challenge blade expand button, which is absent whenever the blade is already open. Only
    # lines naming a type or member are evidence that a binding broke.
    $failurePattern = @(
        'Could not resolve required handles',
        'Failed to initialize .*reflection',
        'Could not find [\w.]+\.[\w<>]+',
        'Could not find \w+ (?:type|setter|getter|method|property|field|fields|methods)\b',
        '[\w.]+\.\w+\(?\)? not found'
    ) -join '|'
    $logHits = @(Select-String -Path $latestLog -Pattern '\[Accessible Arena\]' |
        Where-Object { $_.Line -match $failurePattern } |
        ForEach-Object { ($_.Line -replace '^\[[\d:.]+\]\s*', '').Trim() } |
        Select-Object -Unique)
    Write-Host "0. Resolution failures in the last game session: $($logHits.Count)" -ForegroundColor $(if ($logHits.Count) { "Yellow" } else { "Green" })
    Write-Host "   ($latestLog, $((Get-Item $latestLog).LastWriteTime.ToString('yyyy-MM-dd HH:mm')))"
    foreach ($hit in $logHits) { Write-Host "  $hit" }
} else {
    Write-Host "0. No MelonLoader log found - launch the game once to get runtime coverage." -ForegroundColor DarkGray
}

if ($FromDecompiled) {
    # Reference = decompiled sources captured before the update. Type-scoped, so this is
    # sharper than the global name check, but only covers types that were decompiled.
    $decompiledDir = Join-Path $repoRoot "llm-docs\decompiled"
    if (-not (Test-Path $decompiledDir)) {
        Write-Host ""
        Write-Host "No llm-docs/decompiled/ directory - run tools\decompile-all.ps1 first." -ForegroundColor Yellow
        exit 2
    }
    $dllStamp = (Get-Item (Join-Path $ManagedDir "Core.dll")).LastWriteTime
    $stale = @(Get-ChildItem $decompiledDir -Filter *.cs | Where-Object { $_.LastWriteTime -lt $dllStamp })

    Write-Host ""
    Write-Host "Reference: $($stale.Count) decompiled files written before $($dllStamp.ToString('yyyy-MM-dd HH:mm'))" -ForegroundColor Cyan

    # Member declarations only: the lookahead drops "public sealed class Foo" and the like,
    # so a type declaration is not mistaken for a member named after its type.
    $declPattern = '(?m)^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|protected internal)\s+(?:static\s+|readonly\s+|virtual\s+|override\s+|sealed\s+|abstract\s+|extern\s+|unsafe\s+|new\s+|async\s+|const\s+)*(?!(?:class|struct|enum|interface|delegate|record)\s)[\w<>\[\],\.\?]+\s+(\w+)\s*[\(\{;=]'
    $goneTypes = @()
    $goneMembers = @()
    $unreadable = 0
    $unknownTypes = 0
    $otherChanges = 0
    $fallbackHits = 0

    foreach ($file in $stale) {
        # Filenames carry the decompile source, e.g. "ZenFulcrum.EmbeddedBrowser.Browser.decompiled.cs".
        $typeName = $file.Name -replace '\.cs$', '' -replace '\.decompiled$', ''
        $typeName = $typeName.Split('.')[-1]

        try { $text = [System.IO.File]::ReadAllText($file.FullName) }
        catch { $unreadable++; continue }

        $live = [GameApi]::MembersOfType($typeName)
        if ($live -eq $null) {
            # Only a name the mod actually asks for is a regression; the rest are files
            # named after a concept rather than a type.
            if ($typeRefs.ContainsKey($typeName)) { $goneTypes += $typeName } else { $unknownTypes++ }
            continue
        }
        $liveSet = New-Object System.Collections.Generic.HashSet[string]
        foreach ($n in $live) { [void]$liveSet.Add($n) }

        $used = New-Object System.Collections.Generic.SortedSet[string]
        foreach ($m in [regex]::Matches($text, $declPattern)) {
            $name = $m.Groups[1].Value
            if ($name -eq $typeName) { continue }   # constructor
            if ($liveSet.Contains($name)) { continue }
            if (-not $memberRefs.ContainsKey($name)) { $otherChanges++; continue }
            if (Test-FallbackOnly $name) { $fallbackHits++; continue }
            [void]$used.Add($name)
        }
        if ($used.Count -gt 0) {
            $goneMembers += [pscustomobject]@{ Type = $typeName; Members = @($used) }
        }
    }

    Write-Host "  $($stale.Count - $unreadable) files read, $unknownTypes named after something other than a live type, $unreadable unreadable"
    Write-Host "  $otherChanges member changes in types the mod does not reflect on (ignored)"
    Write-Host "  $fallbackHits gone but only ever asked for as a '??' fallback (ignored)"

    Write-Host ""
    Write-Host "A. Types the mod looks up that no longer exist: $($goneTypes.Count)" -ForegroundColor $(if ($goneTypes.Count) { "Red" } else { "Green" })
    foreach ($t in $goneTypes) {
        $sites = @($typeRefs[$t]) | Select-Object -First 3
        Write-Host "  $t   ($($sites -join ', '))"
    }

    Write-Host ""
    Write-Host "B. Members the mod reflects on that vanished from their type: $($goneMembers.Count) type(s)" -ForegroundColor $(if ($goneMembers.Count) { "Red" } else { "Green" })
    foreach ($f in ($goneMembers | Sort-Object { -$_.Members.Count })) {
        Write-Host ""
        Write-Host "  $($f.Type)"
        $shown = 0
        foreach ($member in $f.Members) {
            if ($shown -ge $MaxDetail) { Write-Host "    ... and $($f.Members.Count - $shown) more"; break }
            $sites = @($memberRefs[$member]) | Select-Object -First 2
            Write-Host "    $member   -> $($sites -join ', ')"
            # Decompiled files often carry more than one type, so a hit can be a member the
            # mod reads off a different component. Say where it lives now instead of guessing.
            $owners = @([GameApi]::OwnersOf($member, 3))
            if ($owners.Count -gt 0) {
                Write-Host "       still exists on: $($owners -join ', ')" -ForegroundColor DarkGray
            } else {
                Write-Host "       gone from every game type" -ForegroundColor Red
            }
            $shown++
        }
    }

    $regressions = $goneTypes.Count + $goneMembers.Count
    Write-Host ""
    Write-Host "A member listed under B may still exist on a different type - the mod resolves most" -ForegroundColor DarkGray
    Write-Host "names against whatever component it found, so check the call site before changing code." -ForegroundColor DarkGray
    exit $(if ($regressions -gt 0) { 1 } else { 0 })
}

# Default mode: compare against the stored baseline.
if (-not (Test-Path $namesFile)) {
    Write-Host ""
    Write-Host "No baseline stored yet." -ForegroundColor Yellow
    Write-Host "  Right after an update, run:  tools\check-game-update.ps1 -FromDecompiled"
    Write-Host "  Once the mod is healthy, run: tools\check-game-update.ps1 -UpdateBaseline"
    Write-Host ""
    Write-Host "Names that resolve to nothing in the current game (no baseline to compare, so this"
    Write-Host "includes long-standing fallbacks that were never expected to resolve):"
    $missing = @($nameStatus.Keys | Where-Object { $nameStatus[$_] -eq "missing" })
    Write-Host "  $($missing.Count) of $($nameStatus.Count)"
    foreach ($key in $missing) {
        $kind, $name = $key.Split(" ", 2)
        $where = Get-Sites $kind $name
        Write-Host "  $kind $name  ($($where[0]))"
    }
    exit 0
}

$baselineStatus = @{}
foreach ($line in [System.IO.File]::ReadAllLines($namesFile)) {
    if ($line -match '^(ok|missing)\s+(member|type)\s+(.+)$') {
        $baselineStatus[$matches[2] + " " + $matches[3]] = $matches[1]
    }
}
$baselineMeta = if (Test-Path $metaFile) { (Get-Content $metaFile)[0] } else { "unknown" }
Write-Host "  baseline:     $baselineMeta"

$broke = @()
$healed = @()
$appeared = @()
foreach ($key in $nameStatus.Keys) {
    if (-not $baselineStatus.ContainsKey($key)) {
        $bareName = ($key -split ' ', 2)[1]
        if ($nameStatus[$key] -eq "missing" -and -not (Test-FallbackOnly $bareName)) { $appeared += $key }
        continue
    }
    if ($baselineStatus[$key] -eq "ok" -and $nameStatus[$key] -eq "missing") { $broke += $key }
    if ($baselineStatus[$key] -eq "missing" -and $nameStatus[$key] -eq "ok") { $healed += $key }
}

Write-Host ""
Write-Host "1. Names that used to resolve and no longer do: $($broke.Count)" -ForegroundColor $(if ($broke.Count) { "Red" } else { "Green" })
foreach ($key in $broke) {
    $kind, $name = $key.Split(" ", 2)
    $where = Get-Sites $kind $name
    Write-Host ""
    Write-Host "  $kind $name" -ForegroundColor Red
    $shown = 0
    foreach ($w in $where) {
        if ($shown -ge $MaxDetail) { Write-Host "    ... and $($where.Count - $shown) more call sites"; break }
        Write-Host "    $w"
        $shown++
    }
}
$regressions += $broke.Count

Write-Host ""
Write-Host "2. New names in the mod that do not resolve: $($appeared.Count)" -ForegroundColor $(if ($appeared.Count) { "Yellow" } else { "Green" })
foreach ($key in $appeared) {
    $kind, $name = $key.Split(" ", 2)
    $where = Get-Sites $kind $name
    Write-Host "  $kind $name  ($($where[0]))"
}

if ($healed.Count -gt 0) {
    Write-Host ""
    Write-Host "3. Names that started resolving (usually harmless): $($healed.Count)" -ForegroundColor DarkGray
    foreach ($key in $healed) { Write-Host "  $key" }
}

# Signature diff for tracked types.
if (Test-Path $snapshotFile) {
    $currentSnapshot = [GameApi]::Snapshot([string[]]@($trackedTypes))
    $oldBlocks = @{}
    $newBlocks = @{}

    function Read-Blocks($lines, $table) {
        $current = $null
        foreach ($line in $lines) {
            if ($line.StartsWith("## ")) {
                $current = $line.Substring(3).Split(" ")[0]
                $table[$current] = New-Object System.Collections.ArrayList
            } elseif ($current -and $line) {
                [void]$table[$current].Add($line)
            }
        }
    }
    Read-Blocks ([System.IO.File]::ReadAllLines($snapshotFile)) $oldBlocks
    Read-Blocks $currentSnapshot $newBlocks

    $changedTypes = @()
    $vanishedTypes = @()
    foreach ($type in $oldBlocks.Keys) {
        if (-not $newBlocks.ContainsKey($type)) { $vanishedTypes += $type; continue }
        $diff = Compare-Object $oldBlocks[$type] $newBlocks[$type]
        if ($diff) { $changedTypes += [pscustomobject]@{ Type = $type; Diff = $diff } }
    }

    Write-Host ""
    Write-Host "4. Tracked game types that disappeared: $($vanishedTypes.Count)" -ForegroundColor $(if ($vanishedTypes.Count) { "Red" } else { "Green" })
    foreach ($type in $vanishedTypes) { Write-Host "  $type" }
    $regressions += $vanishedTypes.Count

    Write-Host ""
    Write-Host "5. Tracked game types whose signatures changed: $($changedTypes.Count)" -ForegroundColor $(if ($changedTypes.Count) { "Yellow" } else { "Green" })
    foreach ($entry in ($changedTypes | Sort-Object Type)) {
        Write-Host ""
        Write-Host "  $($entry.Type)"
        $shown = 0
        foreach ($d in $entry.Diff) {
            if ($shown -ge $MaxDetail) { Write-Host "    ... and $($entry.Diff.Count - $shown) more changes"; break }
            $mark = if ($d.SideIndicator -eq "=>") { "+ added  " } else { "- removed" }
            Write-Host "    $mark $($d.InputObject)"
            $shown++
        }
    }
} else {
    Write-Host ""
    Write-Host "4./5. No signature snapshot stored - run -UpdateBaseline to enable signature diffs." -ForegroundColor Yellow
}

Write-Host ""
if ($regressions -gt 0) {
    Write-Host "$regressions regression(s) need attention." -ForegroundColor Red
    Write-Host "After fixing, re-run with -UpdateBaseline to record the new known-good state."
    exit 1
}
Write-Host "No new breakage detected." -ForegroundColor Green
exit 0
