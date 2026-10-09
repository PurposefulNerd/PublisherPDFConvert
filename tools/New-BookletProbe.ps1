param([string]$OutDir)
# Builds a small facing-pages (book layout) publication: four pages, a
# two-page master with a page-number footer, and a shape crossing the spine.
# Exercises the left/right page and spine-origin math of the IDML export.
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$app = New-Object -ComObject Publisher.Application
try {
    $doc = $app.Documents.Add()
    for ($i = 0; $i -lt 10 -and $null -eq $doc.TextStyles; $i++) { Start-Sleep -Milliseconds 300 }
    $ps = $doc.PageSetup
    try { $ps.PublicationLayout = 2 } catch { "PublicationLayout=book failed: $($_.Exception.Message)" }   # pbLayoutBook
    try { $doc.ViewTwoPageSpread = $true } catch {}
    try { $ps.PageWidth = 396; $ps.PageHeight = 612 } catch { "page size failed: $($_.Exception.Message)" }
    "layout=$($ps.PublicationLayout) size=$($ps.PageWidth)x$($ps.PageHeight) twoPage=$($doc.ViewTwoPageSpread)"

    $master = $doc.MasterPages.Item(1)
    try { $master.IsTwoPageMaster = $true } catch { "two-page master failed: $($_.Exception.Message)" }
    $ft = $master.Shapes.AddTextbox(1, 36, 570, 150, 20); $ft.Name = 'MasterFooter'
    $ft.TextFrame.TextRange.Text = 'Page '
    [void]$ft.TextFrame.TextRange.InsertPageNumber(1)

    while ($doc.Pages.Count -lt 4) { [void]$doc.Pages.Add(1, $doc.Pages.Count) }
    for ($i = 1; $i -le 4; $i++) {
        $p = $doc.Pages.Item($i)
        $t = $p.Shapes.AddTextbox(1, 36, 36, 324, 60); $t.Name = "Title$i"
        $t.TextFrame.TextRange.Text = "Page $i of the booklet (type $($p.PageType))"
        $t.TextFrame.TextRange.Font.Size = 20
        $r = $p.Shapes.AddShape(1, 36, 120, 324, 200); $r.Name = "Box$i"
        $r.Fill.ForeColor.RGB = 0x20 * $i + 0x4000 * $i
    }
    # A shape that straddles the spine on the 2-3 spread: starts on page 2, extends past its right edge.
    $sp = $doc.Pages.Item(2).Shapes.AddShape(9, 300, 400, 192, 100); $sp.Name = 'SpineOval'; $sp.Fill.ForeColor.RGB = 0x00A5FF
    foreach ($i in 1..4) { $p = $doc.Pages.Item($i); "page $i type=$($p.PageType) master=$($p.Master.Name) two=$($p.Master.IsTwoPageMaster)" }
    $out = Join-Path $OutDir 'booklet.pub'
    $doc.SaveAs($out, 1, $false); "saved $out"
    $doc.Close()
}
finally { try { $app.Quit() } catch {} }
