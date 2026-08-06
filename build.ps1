<#
  Builds PublisherToPdf.exe using the .NET Framework C# compiler (csc.exe)
  that ships with Windows. No Visual Studio or .NET SDK required.

  Usage:   powershell -ExecutionPolicy Bypass -File build.ps1
  Output:  bin\PublisherToPdf.exe
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$bin  = Join-Path $root 'bin'
$out  = Join-Path $bin 'PublisherToPdf.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw "Could not find csc.exe. The .NET Framework 4.x runtime is required (built into Windows 10/11)."
}

New-Item -ItemType Directory -Force -Path $bin | Out-Null

$sources = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }
$manifest = Join-Path $src 'app.manifest'

$refs = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:Microsoft.CSharp.dll'
)

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    "/out:$out",
    "/win32manifest:$manifest"
) + $refs + $sources

Write-Host "Compiling with $csc ..." -ForegroundColor Cyan
& $csc $args

if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)." }
Write-Host "Build succeeded: $out" -ForegroundColor Green
