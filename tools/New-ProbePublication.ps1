param([string]$OutDir)
# Builds a synthetic Publisher document with one of each feature we need to
# map, saves it as .pub (current, 2000 and 98 formats), and quits Publisher.
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
Add-Type -AssemblyName System.Drawing

# A small PNG to place.
$png = Join-Path $OutDir 'sample.png'
$bmp = [System.Drawing.Bitmap]::new(200, 120)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::CornflowerBlue)
$g.FillEllipse([System.Drawing.Brushes]::Gold, 20, 10, 160, 100)
$g.Dispose(); $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

$app = New-Object -ComObject Publisher.Application
try {
    "Publisher $($app.Version) build $($app.Build)"
    $doc = $app.Documents.Add()
    for ($i = 0; $i -lt 10 -and $null -eq $doc.TextStyles; $i++) { Start-Sleep -Milliseconds 300 }
    "Document: $($doc.Name) pages=$($doc.Pages.Count) masters=$($doc.MasterPages.Count)"
    "Page size: $($doc.PageSetup.PageWidth) x $($doc.PageSetup.PageHeight) pt"

    # --- Styles --------------------------------------------------------------
    $normal = $doc.TextStyles.Item('Normal')
    $hf = $normal.Font.Duplicate(); $hf.Name = 'Arial'; $hf.Size = 24; $hf.Bold = -1
    $hp = $normal.ParagraphFormat.Duplicate(); $hp.SpaceAfter = 12; $hp.Alignment = 1
    [void]$doc.TextStyles.Add('Probe Heading', 'Normal', $hf, $hp)
    $bf = $normal.Font.Duplicate(); $bf.Name = 'Georgia'; $bf.Size = 11
    $bp = $normal.ParagraphFormat.Duplicate(); $bp.Alignment = 6; $bp.FirstLineIndent = 18; $bp.SetLineSpacing(5, 1.2)
    [void]$doc.TextStyles.Add('Probe Body', 'Normal', $bf, $bp)

    # --- Master page: footer text + rule -------------------------------------
    $master = $doc.MasterPages.Item(1)
    $mt = $master.Shapes.AddTextbox(1, 36, 740, 540, 24)
    $mt.Name = 'MasterFooter'
    $mt.TextFrame.TextRange.Text = 'Master footer - page '
    [void]$mt.TextFrame.TextRange.InsertPageNumber(1)
    $ml = $master.Shapes.AddLine(36, 736, 576, 736); $ml.Name = 'MasterRule'

    # --- Page 1 --------------------------------------------------------------
    $p1 = $doc.Pages.Item(1)

    $t1 = $p1.Shapes.AddTextbox(1, 36, 36, 260, 200); $t1.Name = 'StoryA'
    $tr = $t1.TextFrame.TextRange
    $tr.Text = "Probe Heading`rFirst body paragraph with a bold word and an italic word and a red word.`rSecond paragraph, bulleted.`rThird paragraph overflows into the linked frame so the thread can be tested with enough words to fill it up completely and then some more words after that to be sure."
    $tr.Paragraphs(1,1).ParagraphFormat.TextStyle = 'Probe Heading'
    $tr.Paragraphs(2,3).ParagraphFormat.TextStyle = 'Probe Body'
    $f = $tr.Find; $f.Clear(); $f.FindText = 'bold'; if ($f.Execute()) { $f.FoundTextRange.Font.Bold = -1 }
    $f.Clear(); $f.FindText = 'italic'; if ($f.Execute()) { $f.FoundTextRange.Font.Italic = -1 }
    $f.Clear(); $f.FindText = 'red'; if ($f.Execute()) { $f.FoundTextRange.Font.Color.RGB = 255; $f.FoundTextRange.Font.Underline = 1 }
    $tr.Paragraphs(3,1).ParagraphFormat.SetListType(23, [string][char]0x2022)
    $t1.TextFrame.Columns = 1; $t1.TextFrame.MarginLeft = 6; $t1.TextFrame.MarginTop = 6
    $t1.Fill.ForeColor.RGB = 0xEEF5FF; $t1.Line.Visible = -1; $t1.Line.ForeColor.RGB = 0x804000; $t1.Line.Weight = 1.5

    $t2 = $p1.Shapes.AddTextbox(1, 320, 36, 256, 120); $t2.Name = 'StoryA-cont'
    $t1.TextFrame.NextLinkedTextFrame = $t2.TextFrame

    $pic = $p1.Shapes.AddPicture($png, 0, -1, 320, 180, 200, 120); $pic.Name = 'EmbeddedPNG'
    $pic.TextWrap.Type = 1

    $picL = $p1.Shapes.AddPicture($png, -1, 0, 320, 320, 120, 72); $picL.Name = 'LinkedPNG'
    $picL.PictureFormat.CropLeft = 10; $picL.PictureFormat.CropTop = 5

    $rect = $p1.Shapes.AddShape(5, 36, 260, 120, 80); $rect.Name = 'RoundedRect'
    $rect.Fill.ForeColor.RGB = 0x00C000; $rect.Fill.Transparency = 0.25
    $rect.Line.ForeColor.RGB = 0; $rect.Line.Weight = 3; $rect.Line.DashStyle = 4
    $rect.Rotation = 15

    $oval = $p1.Shapes.AddShape(9, 180, 260, 100, 80); $oval.Name = 'Oval'
    $oval.Fill.TwoColorGradient(1, 1); $oval.Fill.ForeColor.RGB = 0xFF8800; $oval.Fill.BackColor.RGB = 0xFFFFFF

    $line = $p1.Shapes.AddLine(36, 360, 300, 400); $line.Name = 'Line'
    $line.Line.Weight = 2; $line.Line.EndArrowheadStyle = 2

    try {
        $fb = $p1.Shapes.BuildFreeform(1, 36, 420)
        $fb.AddNodes(0, 1, 120, 440)
        $fb.AddNodes(1, 1, 180, 500, 100, 520, 60, 460)
        $fb.AddNodes(0, 1, 36, 420)
        $free = $fb.ConvertToShape(); $free.Name = 'Freeform'; $free.Fill.ForeColor.RGB = 0xCC00CC
    } catch { "freeform failed: $($_.Exception.Message)" }

    $tbl = $p1.Shapes.AddTable(3, 3, 320, 420, 256, 90); $tbl.Name = 'Table'
    for ($r = 1; $r -le 3; $r++) { for ($c = 1; $c -le 3; $c++) {
        $tbl.Table.Cells($r, $c, $r, $c).Item(1).TextRange.Text = "R$r" + "C$c" } }
    $tbl.Table.Cells(1,1,1,3).Merge()
    $tbl.Table.Cells(1,1,1,1).Item(1).Fill.ForeColor.RGB = 0xDDDDDD

    $wa = $p1.Shapes.AddTextEffect(0, 'WordArt', 'Impact', 28, 0, 0, 36, 540); $wa.Name = 'WordArt'

    $g1 = $p1.Shapes.AddShape(1, 320, 540, 60, 40); $g1.Name = 'GroupChildA'
    $g2 = $p1.Shapes.AddShape(9, 390, 540, 60, 40); $g2.Name = 'GroupChildB'
    $grp = $p1.Shapes.Range([object[]]@('GroupChildA','GroupChildB')).Group(); $grp.Name = 'Group'

    $vt = $p1.Shapes.AddTextbox(1, 480, 540, 96, 120); $vt.Name = 'TwoColumns'
    $vt.TextFrame.TextRange.Text = 'Two column text frame with vertical centring and a drop cap.'
    $vt.TextFrame.Columns = 2; $vt.TextFrame.VerticalTextAlignment = 1
    try { $dc = $vt.TextFrame.TextRange.Paragraphs(1,1).DropCap; $dc.LinesUp = 2; $dc.Size = 2; $dc.Span = 1 } catch { "dropcap failed: $($_.Exception.Message)" }
    try { $vt.TextFrame.TextRange.Hyperlinks.Add($vt.TextFrame.TextRange.Words(1,2), 'https://example.com/') | Out-Null } catch { "hyperlink failed: $($_.Exception.Message)" }
    try {
        $pts = New-Object 'single[,]' 4,2
        $pts[0,0]=36;  $pts[0,1]=420; $pts[1,0]=120; $pts[1,1]=440; $pts[2,0]=180; $pts[2,1]=500; $pts[3,0]=36; $pts[3,1]=420
        $pl = $p1.Shapes.AddPolyline($pts); $pl.Name = 'Polyline'; $pl.Fill.ForeColor.RGB = 0xCC00CC
    } catch { "polyline failed: $($_.Exception.Message)" }

    # --- Page 2 --------------------------------------------------------------
    $p2 = $doc.Pages.Add(1, 1)
    $t3 = $p2.Shapes.AddTextbox(1, 36, 36, 540, 100); $t3.Name = 'Page2Text'
    $t3.TextFrame.TextRange.Text = 'Page two. Scheme colour fill below.'
    $sc = $p2.Shapes.AddShape(1, 36, 160, 200, 100); $sc.Name = 'SchemeFill'
    $sc.Fill.ForeColor.SchemeColor = 2
    $cm = $p2.Shapes.AddShape(1, 260, 160, 200, 100); $cm.Name = 'CmykFill'
    $cm.Fill.ForeColor.CMYK.SetCMYK(100, 50, 0, 10)

    [void]$doc.Sections.Add(2); $doc.Sections.Item(2).PageNumberStart = 10

    # --- Save in each format ------------------------------------------------
    $cur = Join-Path $OutDir 'synthetic.pub'
    $doc.SaveAs($cur, 1, $false); "saved $cur"
    foreach ($fmt in @(@{n='synthetic-pub2000.pub';f=3}, @{n='synthetic-pub98.pub';f=2})) {
        $p = Join-Path $OutDir $fmt.n
        try { $doc.SaveAs($p, $fmt.f, $false); "saved $p" } catch { "SaveAs format $($fmt.f) failed: $($_.Exception.Message)" }
    }
    $doc.Close()
}
finally { try { $app.Quit() } catch {} }
