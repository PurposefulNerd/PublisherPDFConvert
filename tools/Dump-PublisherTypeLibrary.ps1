param([string]$OutDir = (Join-Path $PSScriptRoot '..\docs\publisher-object-model'))
# Regenerates docs/publisher-object-model/*.md from the Publisher primary
# interop assembly in the GAC (installed with Office). Nothing is decompiled:
# this is plain .NET reflection over public metadata, the same information the
# VBA Object Browser (Alt+F11, F2 in Publisher) shows.
$ErrorActionPreference = 'Stop'
$office = Get-ChildItem 'C:\Windows\assembly\GAC_MSIL\office' -Recurse -Filter 'office.dll' | Select-Object -First 1
$dll = Get-ChildItem 'C:\Windows\assembly\GAC_MSIL\Microsoft.Office.Interop.Publisher' -Recurse -Filter '*.dll' | Select-Object -First 1
if (-not $dll) { throw 'Microsoft.Office.Interop.Publisher.dll not found in the GAC; is Publisher installed?' }
if ($office) { [void][System.Reflection.Assembly]::LoadFrom($office.FullName) }
$asm = [System.Reflection.Assembly]::LoadFrom($dll.FullName)
try { $types = $asm.GetExportedTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ } }
$types = $types | Sort-Object FullName
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Members($t) {
    foreach ($p in ($t.GetProperties() | Sort-Object Name)) {
        $acc = @(); if ($p.CanRead) { $acc += 'get' }; if ($p.CanWrite) { $acc += 'set' }
        $idx = ($p.GetIndexParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        $name = if ($idx) { "$($p.Name)[$idx]" } else { $p.Name }
        "- prop $name : $($p.PropertyType.Name) {$($acc -join ';')}"
    }
    foreach ($m in ($t.GetMethods() | Where-Object { -not $_.IsSpecialName } | Sort-Object Name)) {
        $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        "- method $($m.Name)($ps) : $($m.ReturnType.Name)"
    }
}

$out = @("# Publisher object model: enums (from $($dll.Name) v$($asm.GetName().Version))")
foreach ($t in $types | Where-Object { $_.IsEnum }) {
    $out += ""; $out += "## $($t.Name)"
    foreach ($n in [Enum]::GetNames($t)) { $out += "- $n = $([int][Enum]::Parse($t, $n))" }
}
$out -join "`n" | Set-Content (Join-Path $OutDir 'enums.md') -Encoding UTF8

$out = @("# Publisher object model: interfaces and members")
foreach ($t in $types | Where-Object { $_.IsInterface -and $_.Name -notmatch 'Events|^_' }) { $out += ""; $out += "## $($t.Name)"; $out += Members $t }
$out -join "`n" | Set-Content (Join-Path $OutDir 'members.md') -Encoding UTF8

# Document and Application expose their members on the hidden _Document / _Application interfaces.
$out = @()
foreach ($n in '_Document', '_Application') { $t = $asm.GetType("Microsoft.Office.Interop.Publisher.$n"); $out += "## $n"; $out += Members $t }
$out -join "`n" | Set-Content (Join-Path $OutDir 'document.md') -Encoding UTF8

"Assembly: $($dll.FullName)"
"Types: $($types.Count)  enums: $(($types | Where-Object IsEnum).Count)"
"Wrote enums.md, members.md, document.md to $OutDir"
