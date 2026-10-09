param([string]$PubFile, [string]$OutDir)
# Does the cold-start race also affect ExportAsFixedFormat (what PublisherToPdf uses)?
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force $OutDir | Out-Null
# Each trial needs a genuinely fresh Publisher process. Close any Publisher you have open before running this.
if (Get-Process MSPUB -ErrorAction SilentlyContinue) { throw 'Publisher is running. Close it first: this test must start its own fresh instances.' }
function Fresh { Get-Process MSPUB -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false; $dl = (Get-Date).AddSeconds(10); while ((Get-Process MSPUB -ErrorAction SilentlyContinue) -and (Get-Date) -lt $dl) { Start-Sleep -Milliseconds 200 }; Start-Sleep -Milliseconds 500; New-Object -ComObject Publisher.Application }
foreach ($mode in 'cold', 'warm') {
    $app = Fresh
    try {
        if ($mode -eq 'warm') { $w = $app.Documents.Add(); $null = $w.TextStyles.Count; $w.Close() }
        $d = $app.Open($PubFile, $true)
        $out = Join-Path $OutDir "$mode.pdf"
        $d.ExportAsFixedFormat(2, $out, 2)
        $txt = $d.Pages.Item(1).Shapes.Item('StoryA').TextFrame.TextRange.Text
        $d.Close()
        "$mode : pdf=$((Get-Item $out).Length) bytes, model text=[$($txt.Substring(0,20))]"
    } catch { "$mode : ERR $($_.Exception.Message)" } finally { try { $app.Quit() } catch {} }
}
# Crude content check: count distinct font resources and look at text-ish operators.
foreach ($mode in 'cold', 'warm') {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $OutDir "$mode.pdf"))
    $s = [Text.Encoding]::Latin1.GetString($bytes)
    $fonts = ([regex]::Matches($s, '/BaseFont\s*/([A-Za-z0-9+#,-]+)') | % { $_.Groups[1].Value } | Sort-Object -Unique) -join ','
    "$mode : fonts=$fonts"
}
