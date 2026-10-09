# Publisher → IDML (→ DesignCraft): feasibility and mapping

> **Status (2026-10-08): implemented.** `src/PublisherReader.cs` + `src/Idml/IdmlWriter.cs`,
> reachable from the UI (Format: IDML / Both) and headlessly
> (`PublisherToPdf.exe --idml in.pub out.idml`). Verified by rendering the probe
> publication in DesignCraft 0.4.0 (`designcraft-cli run --in x.idml --all-pages dir`)
> next to Publisher's own rendering. Sections §1–§3 below are the study that
> preceded it and still describe the mapping the code implements; §2's gotchas
> are all handled in the reader. Found while implementing, in addition:
> - Table cells' paragraph/hyperlink/field offsets are absolute within the table's
>   story, not the cell (`PublisherReader.Story` rebases on the first paragraph).
> - `Cell.Row/Column` are not reliable for merged cells; the reader walks row-major
>   with a coverage map instead.
> - Autoshapes report `HasTextFrame` but accessing the frame raises
>   `E_ACCESSDENIED` (`UnauthorizedAccessException` in .NET) until text is typed in.
> - Office's "horizontal" gradient means horizontal *bands* (the colour runs
>   top to bottom): IDML `GradientFillAngle` −90, not 0.
> - Publisher positions the first line of a frame by its line spacing, not the
>   font's ascent. A 180 pt title in a 172 pt table cell (0.78 sp spacing) fits in
>   Publisher but overflowed in DesignCraft with `FirstBaselineOffset="AscentOffset"`;
>   the writer uses `LeadingOffset` with `MinimumFirstBaselineOffset="0"`.
> - Publisher's "1 sp" line-spacing unit is 1.2 × the point size; `pbLineSpacingMultiple`
>   values are multiples of that, `pbLineSpacingExactly` is points.
> - **DesignCraft applies a frame's `TextWrapPreference` to the frame's own text**
>   (InDesign ignores self-wrap). Publisher gives every text box a default
>   "through" wrap, so small boxes lost all their text (82 of 1094 glyphs on a
>   one-page process map). The writer never puts a wrap on text frames and
>   treats "through" as no wrap on graphics.
> - For rotated or flipped frames Publisher reports line bounds in page
>   coordinates, so `MeasuredLeading` is skipped there (the estimate is used).
> - `Documents.Add()` puts Publisher's main window on screen and there is no
>   `Application.Visible`; `ActiveWindow.Visible = false` hides only the document
>   window. The converter now warms up by opening the first file twice (an open
>   does not raise the frame) and hides any of its own top-level windows with
>   `ShowWindow`. Publisher also runs as two processes and ignores `Quit()` while
>   COM wrappers are alive, so `Dispose` collects, quits, waits 5 s and ends
>   whatever MSPUB processes were not running before it started.
> - `SaveAsPicture` renders a shape *as displayed*: a picture with `Rotation` 90 comes
>   back already rotated, inside the rotated bounding box. Extracted pictures and
>   rasterised shapes are therefore placed unrotated in that box; only a linked
>   original file still gets the frame rotation.
> - `HorizontalFlip`/`VerticalFlip` on a text box do not mirror its text in
>   Publisher; flips are applied to graphics only.
> - Publisher crashes now and then under automation (event log: `MSPUB.EXE`,
>   `mso30win32client.dll`, 0xc0000005). A batch sees it as "RPC server is
>   unavailable"; the app restarts Publisher and retries the file once.
> - Publisher wraps only the text *beneath* a wrapped picture; InDesign and
>   DesignCraft wrap every frame the picture overlaps. A label placed on top of a
>   photo therefore vanished. Each text frame now gets `IgnoreWrap="true"` unless a
>   wrapped graphic stacked above it overlaps it (`IdmlWriter.WrappedFromAbove`).
> - Real-document check: a 15-page, 24 MB reunion yearbook (69 stories, 25 pictures,
>   22 groups, a table, 4 masters) exported in 90 s with no warnings to a 21 MB IDML
>   that DesignCraft renders in about a second. Only visible difference: Publisher
>   building-block headings draw their rule with a tab that has a *line leader*;
>   the tab and leader are exported (`TabList` + `<Tab/>`) but DesignCraft does not
>   draw leaders yet.

Measured on Microsoft Publisher 16.0.20430 (Microsoft 365) with the
`Microsoft.Office.Interop.Publisher` 15.0 interop assembly, on 2026-10-08.
Everything here comes from reflection over the public interop metadata and
from black-box runs of `tools/New-ProbePublication.ps1` and
`tools/Dump-PubObjectModel.ps1`. No Publisher binary was disassembled and no
`.pub` bytes were reverse engineered — Publisher reads its own files and the
object model tells us what is in them.

## 1. Conclusions

1. **The object model is complete enough to write an editable layout file.**
   Every feature in the probe publication (styles, master page, threaded text,
   per-character formatting, bullets, hyperlinks, page-number field, embedded
   and linked pictures with crop, autoshapes, gradient/scheme/CMYK fills,
   lines with arrowheads, polyline nodes, group, table with merged cells,
   WordArt, sections) came back with enough detail to reconstruct it.
2. **File versions are a non-issue through COM.** The same publication saved
   in current, Publisher 2000 and Publisher 98 formats produced identical
   dumps after opening (same styles, stories, runs, threading, fields). The
   downgrade formats drop sections and embed linked pictures, but that is loss
   at *save* time, not something the exporter has to handle. `Document.SaveFormat`
   reports `1` for all three, so the format cannot be detected this way — and
   does not need to be.
3. **There is a cold-start race that silently corrupts everything.** See §2.
   It also affects the existing PDF export and explains the "some text may not
   appear" note that used to be in the README. The converter now warms up.
4. **The biggest mapping gap is on the DesignCraft side, not Publisher's:**
   DesignCraft's IDML importer does not yet read tables, hyperlinks, anchored
   objects or bullet details. Those export fine to IDML (InDesign and Affinity
   read them) and will light up in DesignCraft when its importer catches up.

## 2. Gotchas that cost real time (read before writing code)

| # | Behaviour | Measured | Rule |
|---|-----------|----------|------|
| 1 | **Cold-start race.** A document opened within ~2 s of `Publisher.Application` starting is permanently half-initialised: `TextRange.Text` is `#` per character, `ParagraphsCount` = 1, `Stories.Count` = 0, custom `TextStyles` missing, and `ExportAsFixedFormat` renders that state. | Cold PDF 77 KB / 1 font; warm PDF 172 KB / 8 fonts. Waiting 5 s works; `Documents.Add()` + touch `TextStyles` + `Close()` works in ~3 s; re-opening the file also works. Making the window visible does **not** heal it. | Always warm up once per process before the first `Open`. |
| 2 | `Open(path, ReadOnly:=True)` is fine once warmed. | Identical dumps read-only vs writable. | Open sources read-only. |
| 3 | `Pages.Add(Count, After, DuplicateObjectsOnPage, …)`: passing `0` for the optional page index throws `DISP_E_BADINDEX`. | | Omit optional arguments rather than passing zeros. |
| 4 | `FreeformBuilder.AddNodes` rejects explicit zeros for unused control points, and `DropCap.ApplyCustomDropCap` rejected every argument set tried. | | Use `Shapes.AddPolyline(float[,])` for test geometry; read drop caps via `DropCap.LinesUp/Size/Span`, don't try to create them from automation. |
| 5 | `TextRange.Fields` enumerates as nulls from PowerShell; `Fields.Item(i)` works. | `Count` = 1, `Item(1).Type` = 1 (`pbFieldPageNumber`), `.Result` = "1". | Index collections you need field data from. |
| 6 | Publisher outlives `Application.Quit()` while any RCW is alive. | | Release COM objects, `GC.Collect()`, then kill `MSPUB` after a timeout (the converter's `Dispose` already does the GC part). |
| 7 | Publisher crashed (`RPC server unavailable`) once when a second automation client attached to an instance that was shutting down. | | One Publisher per process; never attach to a running instance. |
| 8 | `Shape.Left/Top/Width/Height` are points (`Object`); `GetLeft(pbUnitPoint=3)` etc. return typed singles. | Identical values. | Use the `Get*` methods; they're unambiguous. |
| 9 | `ColorFormat.RGB` is a BGR-packed `Int32` (`0x00BBGGRR`). `Type` says what the user chose: 1 RGB, 2 scheme (`SchemeColor` index), 3 CMYK (`CMYK.Cyan…`). | Scheme Accent 1 → `#5B9BD5`, `SchemeColor` 2; CMYK(100,50,0,10) → `#A1ADCE`. | Export scheme colours as named swatches, CMYK as process swatches, RGB as RGB swatches. |
| 10 | `Shape.SaveAsPicture` re-encodes; it does not return the original bytes. | Embedded 200×120 PNG came back 200×120 at 72 dpi, pixel-exact but a different file; a cropped picture came back cropped (186×113). | For **linked** pictures copy `LinkFormat.SourceFullName`. For **embedded** ones `SaveAsPicture` at 300 dpi is the only route; expect re-encoding (lossless for PNG, recompressed for JPEG). |
| 11 | Per-character formatting is only reachable as `Characters(i,1).Font`, one COM call per character. | 282 chars ≈ 0.5 s. | Fine for typical publications; for long stories walk words (`Words(i,1)`) first and only split a word when `MajorityFont` differs. |
| 12 | Merged table cells are reported in every spanned position with `Width`/`Height` = span and the same text. | 3-column merge → three cells each `3×1` "R1C1 R1C2 R1C3". | Emit a cell only at its origin; use `Width`/`Height` as column/row span. |
| 13 | `Page.LayoutGuides` margins are `null` on ordinary pages. | Only `Document.LayoutGuides` and master pages carry margins/columns. | Read margins from the document / master. |
| 14 | Page-number fields show as `#` in `Text`. | | Replace the character at `Field.TextRange.Start` with the IDML `AutoPageNumber` special character (`<?ACE 18?>`). |
| 15 | Linked frames: `TextFrame.TextRange` of a continuation frame is empty-ish; the story lives on the first frame (`HasPreviousLink` = false) via `Story.TextRange`. | | Walk stories from head frames only; emit `NextTextFrame` links from `NextLinkedTextFrame.Parent.ID`. |
| 16 | `Documents.Add()` works with no arguments from late-bound callers even though the interop signature demands `(PbWizard, Int32)`. | | Omit optional COM arguments; don't invent values. |

## 3. Feature mapping

"DesignCraft" is what its IDML importer reads today (`crates/idml/src/lib.rs`,
"Supported subset" and "TODO"). "InDesign/Affinity" is the IDML spec itself.

### Document, pages, masters

| Publisher | How to read it | IDML | DesignCraft |
|-----------|----------------|------|-------------|
| Page size, orientation | `PageSetup.PageWidth/PageHeight`, `Orientation` | `DocumentPreference` PageWidth/Height, `FacingPages` from `ViewTwoPageSpread`/`PublicationLayout` = Book | ✅ |
| Margins, columns | `Document.LayoutGuides` (`MarginLeft…`, `Columns`, `ColumnGutterWidth`, `MirrorGuides`), master `Page.LayoutGuides` | `MarginPreference` on each page | ✅ |
| Baseline grid | `LayoutGuides.HorizontalBaseLineSpacing/Offset` | `GridPreference` | ✅ |
| Ruler guides | `Page.RulerGuides` (`Position`, `Type`) | `Guide` | ❌ (ignored) |
| Master pages | `Document.MasterPages`, `Page.Master`, `Page.IsTwoPageMaster`, `Page.IgnoreMaster` | `MasterSpread`, `AppliedMaster` on `Page` | ✅ |
| Pages | `Document.Pages`, `PageID`, `PageIndex`, `PageType` (left/right) | `Spread`/`Page` | ✅ |
| Sections / numbering | `Document.Sections`: `StartPageIndex`, `PageNumberStart`, `PageNumberFormat`, `ContinueNumbersFromPreviousSection` | `Section` | ✅ |
| Page background | `Page.Background.Fill` | Rectangle on master | ✅ (as item) |
| Scratch-area objects | `Document.ScratchArea.Shapes` (also `SurplusShapes`) | Pasteboard items on the spread | ✅ |
| Colour scheme | `Document.ColorScheme.Colors(1..8)`, `ColorsInUse` | `Color` swatches in `Resources/Graphic.xml` | ✅ |
| Spot plates | `Document.Plates` (null on RGB publications) | Spot `Color` | ✅ |

### Text

| Publisher | How to read it | IDML | DesignCraft |
|-----------|----------------|------|-------------|
| Text styles | `Document.TextStyles`: `Name`, `BaseStyle`, `NextParagraphStyle`, `Font`, `ParagraphFormat` | `ParagraphStyle` with `BasedOn`, `NextStyle` (Publisher has no character styles) | ✅ |
| Text frame | `Shape.HasTextFrame`, `TextFrame.Columns/ColumnSpacing/Margin*`, `VerticalTextAlignment`, `Orientation` | `TextFrame` + `TextFramePreference` (columns, insets, `VerticalJustification`) | ✅ |
| Threading | `TextFrame.NextLinkedTextFrame.Parent.ID` / `PreviousLinkedTextFrame` | `NextTextFrame` / `PreviousTextFrame` | ✅ |
| Auto-fit (shrink/grow) | `TextFrame.AutoFitText` | no equivalent; bake frame size | n/a |
| Story text | `TextFrame.Story.TextRange.Text` from the head frame; `\r` = paragraph | `Story` → `ParagraphStyleRange`/`CharacterStyleRange`/`Content`, `<Br/>` | ✅ |
| Paragraph format | `Paragraphs(i,1).ParagraphFormat`: `TextStyle` (name), `Alignment`, `LeftIndent`, `RightIndent`, `FirstLineIndent`, `SpaceBefore/After`, `LineSpacing` + `LineSpacingRule`, `KeepLinesTogether`, `KeepWithNext`, `WidowControl`, `Tabs` | `AppliedParagraphStyle` + local overrides; `Leading` (`LineSpacingRule` 4 exact → points; 5 multiple → auto leading ×) | ✅ |
| Character format | `Characters(i,1).Font`: `Name`, `Size`, `Bold`, `Italic`, `Underline`, `Color`, `AllCaps`, `SmallCaps`, `StrikeThrough`, `SuperScript/SubScript`, `Tracking`, `Kerning`, `Scaling`, `Position` | `CharacterStyleRange` attributes (`AppliedFont`, `PointSize`, `FontStyle`, `Underline`, `FillColor`, `Capitalization`, `Position`, `Tracking`, `HorizontalScale`, `BaselineShift`) | ✅ |
| Bullets / numbering | `ParagraphFormat.ListType` (23 = bullet, 255 = none, else numbered), `ListBulletText`, `ListBulletFontName`, `ListIndent`, `ListNumberStart`, `ListNumberSeparator` | `BulletsAndNumberingListType`, `BulletChar`, `NumberingFormat` | ⚠️ partial (glyph/format details TODO) |
| Tabs | `TabStops`: `Position`, `Alignment`, `Leader` | `TabList` | ✅ |
| Drop cap | `TextRange.DropCap.LinesUp/Size/Span` | `DropCapLines`, `DropCapCharacters` | ✅ |
| Hyperlinks | `TextRange.Hyperlinks`: `Address`, `Range.Start/End`, `TargetType` | `HyperlinkTextSource` + `Hyperlink` | ❌ (TODO) |
| Page number field | `Fields.Item(i).Type` = 1/2/3 | `<?ACE 18?>` / next/previous page number specials | ✅ |
| Date/time, mail-merge fields | `Fields` types 4, 5; `Result` holds the text | plain text (`Result`) | ✅ |
| Inline (anchored) shapes | `TextRange.InlineShapes`, `Shape.IsInline`, `InlineAlignment` | anchored object in story | ❌ (TODO) |
| Vertical East-Asian text, RTL | `TextFrame.Orientation`, `ParagraphFormat.TextDirection` | `StoryPreference` direction | ⚠️ |

### Graphics

| Publisher | How to read it | IDML | DesignCraft |
|-----------|----------------|------|-------------|
| Geometry | `GetLeft/GetTop/GetWidth/GetHeight(pbUnitPoint)`, `Rotation`, `HorizontalFlip`, `VerticalFlip`, `ZOrderPosition` | `PathGeometry` + `ItemTransform` (rotate about centre, flips as negative scale), item order = z-order | ✅ |
| Rectangle, rounded rect, oval | `Type` = 1 with `AutoShapeType` 1 / 5 / 9; `Adjustments` for corner radius | `Rectangle` (+ `CornerOption`), `Oval` | ✅ |
| Other autoshapes (100+ presets) | `AutoShapeType`, `Vertices` (2-D array), `Nodes` | `Polygon` from `Vertices`; or rasterise via `SaveAsPicture` | ✅ as polygon |
| Line | `Type` = 9, `Nodes` (2), `Line.Begin/EndArrowheadStyle/Length/Width` | `GraphicLine` with `LeftLineEnd`/`RightLineEnd` | ✅ |
| Freeform / polyline / curve | `Type` = 5, `Nodes` (`SegmentType` 0 line / 1 curve, `Points`) | `Polygon` with Bézier `PathPointType` | ✅ |
| Group | `Type` = 6, `GroupItems` (children in page coordinates) | `Group` | ✅ |
| Picture (embedded) | `Type` = 13, `PictureFormat.Filename` (original name), `ImageFormat`, `Crop*`, `OriginalWidth/Height`, bytes via `SaveAsPicture` | `Rectangle` containing `Image` with embedded `Contents` or a `Link` to `Links/` | ✅ |
| Picture (linked) | `Type` = 11, `LinkFormat.SourceFullName`, `LinkedFileStatus` | `Link` to the copied file | ✅ |
| Picture crop / scale | `CropLeft…` (points of source image), `HorizontalScale/VerticalScale` | image `ItemTransform` inside the frame | ✅ |
| Picture recolor, brightness, contrast | `PictureFormat.IsRecolored`, `Brightness`, `Contrast` | none; bake via `SaveAsPicture` | n/a |
| Solid fill | `Fill.Type` = 1, `ForeColor`, `Transparency` | `FillColor`, `FillTint`, `TransparencySetting/Opacity` | ✅ |
| Gradient fill | `Fill.Type` = 3, `GradientStyle`, `GradientVariant`, `GradientAngle`, `ForeColor`/`BackColor` | `Gradient` swatch + `GradientFillAngle` | ✅ linear/radial |
| Pattern / texture / picture fill | `Fill.Type` = 2 / 4 / 6 | none; rasterise shape | n/a |
| Stroke | `Line.Visible`, `Weight`, `ForeColor`, `DashStyle`, `CapStyle`, `JoinStyle`, `Style` (double etc.), `InsetPen` | `StrokeWeight`, `StrokeColor`, `StrokeType` (dashed presets), `EndCap`, `EndJoin`, `StrokeAlignment` | ✅ (custom dashes TODO) |
| Shadow | `Shadow.Visible`, `Type`, `Blur`, `OffsetX/Y`, `ForeColor`, `Transparency` | `DropShadowSetting` | ✅ |
| Glow, reflection, soft edge, 3-D, bevel | `Glow`, `Reflection`, `SoftEdge`, `ThreeD` | none; rasterise | n/a |
| Text wrap | `TextWrap.Type` (none/square/tight/through/top-bottom), `Side`, `Distance*` | `TextWrapPreference` (`None`, `BoundingBoxTextWrap`, `Contour`, `JumpObjectTextWrap`) + offsets | ✅ |
| BorderArt | `BorderArt.Exists/Name/Weight` | none; rasterise frame or drop | n/a |
| WordArt | `Type` = 15, `TextEffect.Text/FontName/FontSize/PresetShape` | `SaveAsPicture` → image; optionally also a hidden plain text frame for searchability | ✅ as image |
| Table | `Type` = 18, `Table.Rows/Columns`, `Cells(r,c,r,c).Item(1)`: `TextRange`, `Fill`, `Border*`, `Width`/`Height` (= span), `VerticalTextAlignment`, margins | `Table` in a story (`Cell` with `ColumnSpan`/`RowSpan`) | ❌ (TODO) → fallback: one text frame per cell plus ruled lines |
| OLE objects, charts, media, form controls | `Type` 3, 7, 10, 12, 16, 100+ | `SaveAsPicture` → image | ✅ as image |
| Wizard / building-block / catalog-merge shapes | `WizardTag`, `Type` 108, 111 | treat as ordinary shapes (they are) | ✅ |
| Hyperlink on a shape | `Shape.Hyperlink.Address` | `HyperlinkPageItemSource` | ❌ (TODO) |
| Alt text, name | `AlternativeText`, `Name` | `Label` / `Name` | ✅ |

## 4. Recommended exporter design

Add an **"Export as IDML"** mode next to PDF in `PublisherConverter`:

1. `WarmUp()` (already in place), `Open(path, ReadOnly:=true)`.
2. Walk the document into a small C# model (`PubDocument` → `PubPage` →
   `PubShape`, `PubStory`, `PubStyle`, `PubSwatch`). `tools/Dump-PubObjectModel.ps1`
   is that walk in script form and doubles as the oracle: the exporter is right
   when its model equals the dump.
3. Write the IDML package with `System.IO.Compression`: the uncompressed
   `mimetype` entry first, then `designmap.xml`, `Resources/{Graphic,Fonts,Styles,Preferences}.xml`,
   `MasterSpreads/*.xml`, `Spreads/*.xml`, `Stories/*.xml`, `META-INF/container.xml`,
   and `Links/` for pictures. The IDML spec (Adobe, public) defines every element.
   Coordinates: IDML spread origin is the spine / spread centre with y centred on
   the pages — translate from Publisher's top-left page origin.
4. Validate each probe feature by opening the IDML in DesignCraft:
   `designcraft-cli run --in out.idml --export page.png` (headless), and in
   Affinity Publisher / InDesign if available.
5. Shapes with no vector equivalent (pattern fills, BorderArt, 3-D, OLE,
   WordArt) go through `SaveAsPicture(path, 300 dpi)` and are placed as images
   at the shape's bounds, so nothing is lost visually.

Suggested order (each step is testable with the probe publication):
pages + masters + rectangles/ovals/lines (1–2 days) → text frames, styles,
runs, threading (3–5 days) → pictures with crop (1–2 days) → groups, polygons,
fills/strokes/shadows/wrap (2–3 days) → tables, bullets, hyperlinks, fields
(2–4 days) → rasterised fallbacks and a batch UI toggle (1–2 days). Roughly
three working weeks for a solid first release.

## 5. What a native `.pub` importer in DesignCraft would still need

Only if Publisher-free import becomes a goal. The container is documented
([MS-CFB]) and the drawing records are documented ([MS-ODRAW] "Escher"), but the
page, text and style streams (`Contents`, `Quill`) are not. The only prior art,
libmspub (LibreOffice, MPL), cannot be read by anyone contributing to
DesignCraft's clean room. The legitimate route is black-box: minimal
publications saved from Publisher (`tools/New-ProbePublication.ps1` already
writes current, 2000 and 98 formats), one feature changed at a time, and hex
diffs. Budget months, not weeks — and note that Publisher's own "Save As
Publisher 98/2000" is lossy (sections dropped, linked pictures embedded), so
those files are not faithful samples of what old Publisher versions wrote.
