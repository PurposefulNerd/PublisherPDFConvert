param(
    [string]$PubFile,
    [string]$OutDir,
    [string]$DesignCraftCli = '',
    [switch]$SkipBuild
)
# End-to-end check of the IDML export: build the converter, export one .pub
# headlessly, then (if designcraft-cli.exe is available) open the IDML in
# DesignCraft and render every page to PNG for eyeballing.
#
# designcraft-cli.exe ships in the DesignCraft Windows portable zip:
#   https://github.com/storytold/designcraft/releases/latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & powershell -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1') | Out-Null }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$stem = [IO.Path]::GetFileNameWithoutExtension($PubFile)
$idml = Join-Path $OutDir "$stem.idml"
Remove-Item $idml, "$idml.log" -ErrorAction SilentlyContinue
Get-Process MSPUB -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
$p = Start-Process -FilePath (Join-Path $root 'bin\PublisherToPdf.exe') -ArgumentList @('--idml', "`"$PubFile`"", "`"$idml`"") -PassThru -Wait
Get-Process MSPUB -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
"export exit code: $($p.ExitCode)"
if (Test-Path "$idml.log") { Get-Content "$idml.log" }
if ($p.ExitCode -ne 0) { exit 1 }
"idml: $idml ($((Get-Item $idml).Length) bytes)"

if ($DesignCraftCli -and (Test-Path $DesignCraftCli)) {
    $pages = Join-Path $OutDir "$stem-pages"
    New-Item -ItemType Directory -Force $pages | Out-Null
    Remove-Item "$pages\*.png" -ErrorAction SilentlyContinue
    & $DesignCraftCli run --in $idml --all-pages $pages
    Get-ChildItem $pages | ForEach-Object { "rendered: $($_.FullName)" }
}
