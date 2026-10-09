# Publisher to PDF / IDML Converter

A small Windows desktop app (C# / WinForms) that converts one or many Microsoft
Publisher (`.pub`) files to **PDF** (for reading) or **IDML** (an editable layout
that DesignCraft, Adobe InDesign and Affinity Publisher open). Pick your files,
choose a format and where to save, click **Convert**.

Microsoft is retiring Publisher in October 2026. PDF is a one-way street; IDML
lets you keep editing your publications in another application.

![Windows 10 / 11](https://img.shields.io/badge/Windows-10%20%2F%2011-blue)

**Download:** the ready-built `PublisherToPdf.exe` is attached to the
[latest release](https://github.com/PurposefulNerd/PublisherPDFConvert/releases/latest).
No installation; it needs only Windows 10/11 and Microsoft Publisher.

## What it does

- Add **one or several** `.pub` files (button, or drag-and-drop files/folders onto the window)
- Choose **PDF**, **IDML** (an editable layout file that [DesignCraft](https://github.com/storytold/designcraft),
  Adobe InDesign and Affinity Publisher open) or **both**
- Save each output **next to the original**, or send them all to **one output folder**
- Batch progress, per-file status (Done / Failed), and optional "open the folder when finished"
- Faithful output — layout, colors, images, and fonts are rendered by Publisher itself
- **Fit page to content** (on by default): if a publication has items sitting past the
  page edge (on Publisher's scratch area), the page is grown to enclose everything so
  nothing is clipped. Files whose content is already inside the page are exported
  unchanged. Fitting is done on a temporary copy — **your source files are never modified**.

## Requirement: Microsoft Publisher must be installed

`.pub` is a closed, proprietary format. The only way to render it with full
fidelity is to drive **Microsoft Publisher's own "Export as PDF"** engine, so
**Publisher must be installed** on the machine that runs this app. The app
detects this and shows a clear message if Publisher isn't found. No other
software, SDK, or NuGet package is required.

## Build

You do **not** need Visual Studio or the .NET SDK. The app builds with the C#
compiler that already ships with Windows.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

This produces `bin\PublisherToPdf.exe`. Double-click it to run.

Close the app before rebuilding: while it is running, `bin\PublisherToPdf.exe`
is locked and the build fails with "cannot access the file".

### Alternative: build with the .NET SDK / Visual Studio

If you have them installed:

```powershell
dotnet build -c Release
```

(or open `PublisherToPdf.csproj` in Visual Studio and press F5).

## Usage

1. Run `bin\PublisherToPdf.exe`.
2. Click **Add Files…** (or drag `.pub` files onto the window).
3. Choose the **Format**: *PDF*, *IDML* or *Both*. ("Fit page to content" applies
   to PDF only.)
4. Choose the output location:
   - *Save each output next to its original* (default), or
   - *Save all output to this folder* and browse to a folder.
5. Click **Convert**. Watch the per-file status and progress bar.
6. Open the `.idml` in DesignCraft (File ▸ Open), InDesign or Affinity Publisher.
   The fonts used by the publication must be installed on that machine; they are
   referenced by name, not embedded.

Each `document.pub` becomes `document.pdf` and/or `document.idml`. Existing files
with the same name are overwritten. Rows that say **Done (n notes)** have a
tooltip listing what the IDML export had to approximate (for example WordArt
exported as a picture).

### Headless use

```powershell
bin\PublisherToPdf.exe --pdf  in.pub out.pdf
bin\PublisherToPdf.exe --idml in.pub out.idml [--tables-as-table] [--dump-model]
bin\PublisherToPdf.exe --batch-idml outDir [--both] [--twice] a.pub b.pub ...
```

The app has no console; it writes `out.<ext>.log` (first line `OK` or `FAILED`,
then the notes) and returns 0 or 1. `--dump-model` also writes the intermediate
model as `out.idml.model.json`, which `tools\Dump-PubObjectModel.ps1` lets you
compare against what Publisher itself reports. `--batch-idml` converts several
files in one Publisher session exactly as the window does (`--both` adds the
PDF, `--twice` runs the batch two times in one process) and writes
`outDir\batch.log`.

## IDML export: what survives

The exporter reads the publication through Publisher's automation object model
(the same interface that drives the PDF export) and writes an IDML package.
Measured against a probe publication with one of every feature, rendered by
DesignCraft; see [`docs/publisher-to-idml-mapping.md`](docs/publisher-to-idml-mapping.md)
for the full table.

| Kept as editable objects | Approximated |
|---|---|
| Pages, master pages, page size, margins, columns, sections and page numbering (live page-number fields) | WordArt, OLE objects, charts, media, form controls: exported as pictures at their bounds |
| Text frames with columns, insets, vertical alignment and threading across frames | Pattern, texture and picture fills, BorderArt, 3-D: the shape is exported as a picture |
| Text styles (based-on / next style), paragraph formatting, bullets and numbering, tabs, drop caps | Embedded pictures are re-encoded by Publisher (PNG stays lossless; JPEG is recompressed) |
| Per-character formatting: font, size, bold/italic, underline, colour, caps, super/subscript, tracking, scaling, baseline shift | Tables: by default each cell becomes its own text frame (what DesignCraft shows today); `--tables-as-table` writes real IDML tables for InDesign / Affinity |
| Hyperlinks (InDesign / Affinity; DesignCraft keeps the text) | Fonts are referenced by name, not embedded: install them on the receiving machine |
| Facing pages (two-page spreads, items across the spine), two-page masters | Tab leaders (the rule under Publisher's building-block headings) are exported but DesignCraft does not draw them yet |
| Rectangles, rounded rectangles, ovals, lines with arrowheads, freeforms and polygons, groups | |
| Solid, gradient, scheme and CMYK colours as named swatches; strokes with dash, cap and join; opacity; drop shadows; text wrap | |
| Pictures (embedded and linked) with crop | |

## Project layout

| Path | Purpose |
|------|---------|
| `src/Program.cs` | App entry point |
| `src/MainForm.cs` | The window and all UI / batch logic |
| `src/PublisherConverter.cs` | Publisher COM automation: warm-up, PDF export, IDML export entry point |
| `src/PublisherReader.cs` | Walks an open publication through the object model into `PubDocument` |
| `src/Model/PubModel.cs` | The intermediate model (pages, shapes, stories, styles, swatches) |
| `src/Idml/IdmlWriter.cs` | Writes `PubDocument` as an IDML package |
| `src/app.manifest` | High-DPI awareness + Windows 10/11 compatibility |
| `build.ps1` | One-command build using the in-box `csc.exe` |
| `PublisherToPdf.csproj` | Optional project file for the .NET SDK / Visual Studio |

## Notes & troubleshooting

- **"Publisher not found"** — install Microsoft Publisher (included with some
  Microsoft 365 / Office plans), then retry.
- **A file shows "Failed"** — hover the row to see the reason (e.g. a corrupt or
  password-protected file); full details, including the stack trace, are appended
  to `%TEMP%\PublisherToPdf-errors.log`. The batch continues with the remaining files.
- **Publisher crashes under automation now and then** (the Windows event log shows
  `MSPUB.EXE` faulting in `mso30win32client.dll`). The app notices ("RPC server is
  unavailable"), starts a fresh Publisher and retries the file once; such retries
  are noted in the error log.
- Your source files are never modified: plain conversions open the `.pub` read-only,
  and "Fit page to content" works on a temporary copy.
- Converting many large files launches Publisher once for the whole batch and
  closes it at the end, which is much faster than one-at-a-time.
- **Missing or garbled text (fixed).** Earlier builds could produce PDFs with text
  boxes missing or rendered as `####`. The cause was a Publisher cold-start race:
  a file opened within a second or two of the automation server starting comes
  back half-initialised (every character reads as `#`, custom text styles are
  gone) and the PDF engine faithfully renders that broken state. The app now
  opens the first file once as a throwaway before converting it (the second open
  is fine). `tools\Test-ColdStartRace.ps1` reproduces the problem on demand.
- **Publisher stays hidden and exits.** Automation can put Publisher's window on
  screen and Publisher ignores `Quit` while a document is referenced; the app
  hides its own Publisher windows and ends its own Publisher processes when the
  batch finishes. A Publisher you had open yourself is never touched.

## Developing the IDML export

The study that led to the exporter, the measured behaviour of Publisher's
object model (including its traps) and the feature-by-feature mapping are in
[`docs/publisher-to-idml-mapping.md`](docs/publisher-to-idml-mapping.md).

| Path | Purpose |
|------|---------|
| `docs/publisher-object-model/` | Every enum, interface and member of the Publisher automation API (generated from the interop assembly) |
| `tools/Dump-PublisherTypeLibrary.ps1` | Regenerates the folder above |
| `tools/New-ProbePublication.ps1` | Builds a synthetic `.pub` with one of every feature (and Publisher 2000 / 98 copies) |
| `tools/Dump-PubObjectModel.ps1` | Walks any `.pub` through COM and writes everything the object model reports as JSON, extracting pictures: the oracle for `PublisherReader` |
| `tools/New-BookletProbe.ps1` | Builds a four-page facing-pages `.pub` with a two-page master and a shape across the spine |
| `tools/Test-IdmlExport.ps1` | Builds, exports one `.pub` to IDML and, given `designcraft-cli.exe` (from the DesignCraft Windows portable zip), renders every page to PNG |
| `tools/Test-ColdStartRace.ps1` | Reproduces the cold-start race against a `.pub` (cold vs warmed PDF export) |

The test loop is: `tools\New-ProbePublication.ps1 -OutDir probe`, then
`tools\Test-IdmlExport.ps1 -PubFile probe\synthetic.pub -OutDir out -DesignCraftCli <path>\designcraft-cli.exe`,
then look at `out\synthetic-pages\*.png` next to Publisher's own rendering.
