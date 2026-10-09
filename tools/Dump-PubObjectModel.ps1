param([string]$PubFile, [string]$OutJson, [string]$PicDir)
# Opens a .pub read-only through Publisher COM and dumps everything the object
# model reports that an IDML exporter would need. Proves what is reachable
# before any exporter code is written.
$ErrorActionPreference = 'Stop'
if ($PicDir) { New-Item -ItemType Directory -Force $PicDir | Out-Null }

function Try-Get([scriptblock]$sb) { try { & $sb } catch { "ERR: $($_.Exception.Message)" } }
function Rgb([int]$bgr) { '#{0:X2}{1:X2}{2:X2}' -f ($bgr -band 0xFF), (($bgr -shr 8) -band 0xFF), (($bgr -shr 16) -band 0xFF) }
function Color($c) {
    if ($null -eq $c) { return $null }
    $o = [ordered]@{ type = Try-Get { $c.Type }; rgb = Try-Get { Rgb $c.RGB } }
    try { if ($c.Type -eq 2) { $o.scheme = $c.SchemeColor } } catch {}
    try { if ($c.Type -eq 3) { $k = $c.CMYK; $o.cmyk = @($k.Cyan, $k.Magenta, $k.Yellow, $k.Black) } } catch {}
    try { $o.tint = $c.TintAndShade; $o.transparency = $c.Transparency } catch {}
    $o
}
function FontInfo($f) {
    [ordered]@{ name = $f.Name; size = $f.Size; bold = $f.Bold; italic = $f.Italic; underline = $f.Underline
        color = Color $f.Color; allcaps = $f.AllCaps; smallcaps = $f.SmallCaps; strike = $f.StrikeThrough
        super = $f.SuperScript; sub = $f.SubScript; tracking = $f.Tracking; kerning = $f.Kerning; scaling = $f.Scaling; position = $f.Position }
}
function ParaInfo($pf) {
    [ordered]@{ style = Try-Get { [string]$pf.TextStyle }; align = $pf.Alignment; leftIndent = $pf.LeftIndent; rightIndent = $pf.RightIndent
        firstLine = $pf.FirstLineIndent; spaceBefore = $pf.SpaceBefore; spaceAfter = $pf.SpaceAfter
        lineSpacing = $pf.LineSpacing; lineRule = $pf.LineSpacingRule; listType = $pf.ListType; bullet = Try-Get { $pf.ListBulletText }
        keepLines = $pf.KeepLinesTogether; keepNext = $pf.KeepWithNext; widow = $pf.WidowControl
        tabs = @(Try-Get { foreach ($t in $pf.Tabs) { [ordered]@{ pos = $t.Position; align = $t.Alignment; leader = $t.Leader } } }) }
}
function FontKey($f) { "$($f.Name)|$($f.Size)|$($f.Bold)|$($f.Italic)|$($f.Underline)|$($f.Color.RGB)|$($f.SuperScript)|$($f.SubScript)|$($f.AllCaps)|$($f.SmallCaps)" }
function TextInfo($tr, [int]$maxChars = 2000) {
    $o = [ordered]@{ text = $tr.Text; length = $tr.Length; paragraphs = @(); runs = @(); hyperlinks = @(); fields = @(); inlineShapes = Try-Get { $tr.InlineShapes.Count } }
    $pc = $tr.ParagraphsCount
    for ($i = 1; $i -le $pc; $i++) {
        $p = $tr.Paragraphs($i, 1)
        $pi = ParaInfo $p.ParagraphFormat
        $pi.start = $p.Start; $pi.end = $p.End
        try { $dc = $p.DropCap; if ($dc.LinesUp -gt 0) { $pi.dropCap = [ordered]@{ lines = $dc.LinesUp; size = $dc.Size; span = $dc.Span; font = $dc.FontName } } } catch {}
        $o.paragraphs += $pi
    }
    # Character runs: walk characters, collapse equal formatting.
    $n = [Math]::Min($tr.Length, $maxChars)
    $cur = $null; $start = 1
    for ($i = 1; $i -le $n; $i++) {
        $f = $tr.Characters($i, 1).Font
        $k = FontKey $f
        if ($k -ne $cur) {
            if ($cur) { $o.runs += [ordered]@{ start = $start; len = $i - $start; font = $prev } }
            $cur = $k; $start = $i; $prev = FontInfo $f
        }
    }
    if ($cur) { $o.runs += [ordered]@{ start = $start; len = $n - $start + 1; font = $prev } }
    try { foreach ($h in $tr.Hyperlinks) { $o.hyperlinks += [ordered]@{ address = $h.Address; text = $h.TextToDisplay; start = $h.Range.Start; end = $h.Range.End; target = $h.TargetType } } } catch {}
    # Fields' COM enumerator yields nulls from PowerShell; index it instead.
    try { $fc = $tr.Fields.Count; for ($i = 1; $i -le $fc; $i++) { $fl = $tr.Fields.Item($i); $o.fields += [ordered]@{ type = $fl.Type; code = Try-Get { $fl.Code }; result = Try-Get { $fl.Result }; start = Try-Get { $fl.TextRange.Start } } } } catch {}
    $o
}
function FillInfo($fl) {
    $o = [ordered]@{ visible = $fl.Visible; type = $fl.Type; fore = Color $fl.ForeColor; back = Color $fl.BackColor; transparency = $fl.Transparency }
    try { if ($fl.Type -eq 3) { $o.gradient = [ordered]@{ style = $fl.GradientStyle; variant = $fl.GradientVariant; angle = $fl.GradientAngle; colorType = $fl.GradientColorType; degree = $fl.GradientDegree } } } catch {}
    try { if ($fl.Type -eq 6) { $o.textureName = $fl.TextureName } } catch {}
    $o
}
function LineInfo($ln) {
    [ordered]@{ visible = $ln.Visible; weight = $ln.Weight; fore = Color $ln.ForeColor; dash = $ln.DashStyle; style = $ln.Style
        cap = Try-Get { $ln.CapStyle }; join = Try-Get { $ln.JoinStyle }; beginArrow = $ln.BeginArrowheadStyle; endArrow = $ln.EndArrowheadStyle; transparency = $ln.Transparency }
}
function ShapeInfo($s, [string]$pageTag) {
    $o = [ordered]@{ id = $s.ID; name = $s.Name; type = $s.Type; autoShapeType = Try-Get { $s.AutoShapeType }
        left = $s.GetLeft(3); top = $s.GetTop(3); width = $s.GetWidth(3); height = $s.GetHeight(3)
        rotation = $s.Rotation; flipH = $s.HorizontalFlip; flipV = $s.VerticalFlip; z = $s.ZOrderPosition
        isInline = Try-Get { $s.IsInline }; altText = Try-Get { $s.AlternativeText } }
    $o.fill = Try-Get { FillInfo $s.Fill }
    $o.line = Try-Get { LineInfo $s.Line }
    try { $sh = $s.Shadow; if ($sh.Visible) { $o.shadow = [ordered]@{ type = $sh.Type; blur = $sh.Blur; dx = $sh.OffsetX; dy = $sh.OffsetY; color = Color $sh.ForeColor; transparency = $sh.Transparency } } } catch {}
    try { $w = $s.TextWrap; $o.wrap = [ordered]@{ type = $w.Type; side = $w.Side; auto = $w.DistanceAuto; l = $w.DistanceLeft; t = $w.DistanceTop; r = $w.DistanceRight; b = $w.DistanceBottom } } catch {}
    try { if ($s.BorderArt.Exists) { $o.borderArt = [ordered]@{ name = $s.BorderArt.Name; weight = $s.BorderArt.Weight } } } catch {}
    try { $a = @(); foreach ($i in 1..$s.Adjustments.Count) { $a += $s.Adjustments.Item($i) }; if ($a.Count) { $o.adjustments = $a } } catch {}

    if ($s.Type -eq 6) {   # group
        $o.children = @(); foreach ($c in $s.GroupItems) { $o.children += ShapeInfo $c $pageTag }
        return $o
    }
    if ($s.Type -in 5, 9 -or $s.Nodes.Count -gt 0) {   # freeform / line: nodes
        try { $o.nodes = @(foreach ($nd in $s.Nodes) { $pts = $nd.Points; [ordered]@{ seg = $nd.SegmentType; edit = $nd.EditingType; x = $pts[1,1]; y = $pts[1,2] } }) } catch { $o.nodes = "ERR $($_.Exception.Message)" }
        try { $v = $s.Vertices; $o.vertexCount = $v.GetLength(0) } catch {}
    }
    if ($s.HasTextFrame -and $s.Type -ne 18) {
        $tf = $s.TextFrame
        $o.textFrame = [ordered]@{ columns = $tf.Columns; columnSpacing = $tf.ColumnSpacing; ml = $tf.MarginLeft; mt = $tf.MarginTop; mr = $tf.MarginRight; mb = $tf.MarginBottom
            valign = $tf.VerticalTextAlignment; autofit = $tf.AutoFitText; orientation = $tf.Orientation; overflowing = $tf.Overflowing
            hasNext = $tf.HasNextLink; hasPrev = $tf.HasPreviousLink
            nextShapeId = Try-Get { if ($tf.HasNextLink) { $tf.NextLinkedTextFrame.Parent.ID } }
            prevShapeId = Try-Get { if ($tf.HasPreviousLink) { $tf.PreviousLinkedTextFrame.Parent.ID } }
            storyType = Try-Get { $tf.Story.Type } }
        # Text of the whole story (only from the first frame) vs this frame's visible range.
        $o.frameText = Try-Get { $tf.TextRange.Text }
        if (-not $tf.HasPreviousLink) { $o.story = Try-Get { TextInfo $tf.Story.TextRange } }
    }
    if ($s.Type -eq 18 -or $s.HasTable) {
        $t = $s.Table
        $o.table = [ordered]@{ rows = $t.Rows.Count; cols = $t.Columns.Count; growToFit = $t.GrowToFitText; direction = $t.TableDirection
            colWidths = @(foreach ($c in $t.Columns) { $c.Width }); rowHeights = @(foreach ($r in $t.Rows) { $r.Height }); cells = @() }
        foreach ($r in 1..$t.Rows.Count) { foreach ($c in 1..$t.Columns.Count) {
            $cell = $t.Cells($r, $c, $r, $c).Item(1)
            $ci = [ordered]@{ r = $r; c = $c; w = $cell.Width; h = $cell.Height; text = Try-Get { $cell.TextRange.Text }
                fill = Try-Get { Color $cell.Fill.ForeColor }; valign = $cell.VerticalTextAlignment
                borders = Try-Get { [ordered]@{ t = $cell.BorderTop.Weight; b = $cell.BorderBottom.Weight; l = $cell.BorderLeft.Weight; r = $cell.BorderRight.Weight } } }
            $o.table.cells += $ci } }
    }
    if ($s.Type -in 11, 13) {
        $pf = $s.PictureFormat
        $o.picture = [ordered]@{ filename = $pf.Filename; format = $pf.ImageFormat; linked = $pf.IsLinked; linkStatus = Try-Get { $pf.LinkedFileStatus }
            source = Try-Get { $s.LinkFormat.SourceFullName }
            cropL = $pf.CropLeft; cropT = $pf.CropTop; cropR = $pf.CropRight; cropB = $pf.CropBottom
            origW = $pf.OriginalWidth; origH = $pf.OriginalHeight; origRes = $pf.OriginalResolution; effRes = $pf.EffectiveResolution
            fileSize = $pf.FileSize; origFileSize = $pf.OriginalFileSize; hasAlpha = $pf.HasAlphaChannel; transparentBg = $pf.TransparentBackground
            brightness = $pf.Brightness; contrast = $pf.Contrast; recolored = $pf.IsRecolored }
        if ($PicDir) {
            $out = Join-Path $PicDir ("{0}-shape{1}.png" -f $pageTag, $s.ID)
            $o.picture.saved = Try-Get { $s.SaveAsPicture($out, 3); (Get-Item $out).Length }
        }
    }
    if ($s.Type -eq 15) {
        $te = $s.TextEffect
        $o.wordArt = [ordered]@{ text = $te.Text; font = $te.FontName; size = $te.FontSize; bold = $te.FontBold; italic = $te.FontItalic; preset = $te.PresetTextEffect; shape = $te.PresetShape; wordArt = Try-Get { $te.PresetWordArt } }
    }
    if ($s.Type -in 7, 10) { $o.ole = [ordered]@{ progId = Try-Get { $s.OLEFormat.ProgId } } }
    $o
}
function PageInfo($p, [string]$tag) {
    $o = [ordered]@{ id = $p.PageID; index = $p.PageIndex; name = $p.Name; number = Try-Get { $p.PageNumber }; type = $p.PageType
        width = $p.Width; height = $p.Height; isTwoPageMaster = $p.IsTwoPageMaster; ignoreMaster = Try-Get { $p.IgnoreMaster }
        master = Try-Get { $p.Master.Name }; masterId = Try-Get { $p.Master.PageID }
        background = Try-Get { if ($p.Background.Exists) { FillInfo $p.Background.Fill } }
        guides = Try-Get { $lg = $p.LayoutGuides; [ordered]@{ ml = $lg.MarginLeft; mt = $lg.MarginTop; mr = $lg.MarginRight; mb = $lg.MarginBottom; cols = $lg.Columns; gutter = $lg.ColumnGutterWidth; rows = $lg.Rows } }
        rulerGuides = @(Try-Get { foreach ($g in $p.RulerGuides) { [ordered]@{ pos = $g.Position; type = $g.Type } } })
        shapes = @() }
    foreach ($s in $p.Shapes) { $o.shapes += ShapeInfo $s $tag }
    $o
}

$preexisting = @(Get-Process MSPUB -ErrorAction SilentlyContinue | ForEach-Object Id)
$app = New-Object -ComObject Publisher.Application
try {
    # IMPORTANT: a document opened within ~2 s of launching Publisher comes back
    # half-initialised: every character reads as '#', ParagraphsCount is 1,
    # Stories.Count is 0 and custom TextStyles are missing - and it never heals.
    # Creating and closing a blank publication first warms the text engine up.
    $warm = $app.Documents.Add(); $null = $warm.TextStyles.Count; $warm.Close()
    $doc = $app.Open($PubFile, $true)
    $d = [ordered]@{
        file = $PubFile; publisherVersion = $app.Version; build = $app.Build
        saveFormat = Try-Get { $doc.SaveFormat }; publicationType = $doc.PublicationType; colorMode = Try-Get { $doc.ColorMode }
        pageSetup = Try-Get { $ps = $doc.PageSetup; [ordered]@{ w = $ps.PageWidth; h = $ps.PageHeight; orientation = $ps.Orientation; layout = $ps.PublicationLayout; sizeName = Try-Get { $ps.PageSize.Name } } }
        layoutGuides = Try-Get { $lg = $doc.LayoutGuides; [ordered]@{ ml = $lg.MarginLeft; mt = $lg.MarginTop; mr = $lg.MarginRight; mb = $lg.MarginBottom; cols = $lg.Columns; gutter = $lg.ColumnGutterWidth; mirror = $lg.MirrorGuides } }
        colorScheme = Try-Get { $cs = $doc.ColorScheme; [ordered]@{ name = $cs.Name; colors = @(foreach ($i in 1..8) { Try-Get { Rgb $cs.Colors($i).RGB } }) } }
        textStyles = @(foreach ($t in $doc.TextStyles) { [ordered]@{ name = $t.Name; base = $t.BaseStyle; next = Try-Get { $t.NextParagraphStyle }; font = FontInfo $t.Font; para = ParaInfo $t.ParagraphFormat } })
        sections = @(foreach ($s in $doc.Sections) { [ordered]@{ startPage = $s.StartPageIndex; numberStart = $s.PageNumberStart; format = $s.PageNumberFormat; continue = $s.ContinueNumbersFromPreviousSection } })
        storiesCount = Try-Get { $doc.Stories.Count }
        masterPages = @(); pages = @()
    }
    $i = 0; foreach ($m in $doc.MasterPages) { $i++; $d.masterPages += PageInfo $m "master$i" }
    foreach ($p in $doc.Pages) { $d.pages += PageInfo $p "page$($p.PageIndex)" }
    $doc.Close()
    $d | ConvertTo-Json -Depth 14 | Set-Content $OutJson -Encoding UTF8
    "wrote $OutJson ($((Get-Item $OutJson).Length) bytes)"
}
finally {
    # Publisher often outlives Quit() while COM wrappers are alive; release them, then force it.
    try { $app.Quit() } catch {}
    $doc = $null; $app = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    # Only the instance this script started is killed; a Publisher the user has open is left alone.
    $mine = { Get-Process MSPUB -ErrorAction SilentlyContinue | Where-Object { $preexisting -notcontains $_.Id } }
    $deadline = (Get-Date).AddSeconds(10)
    while ((& $mine) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
    & $mine | Stop-Process -Force -Confirm:$false
}
