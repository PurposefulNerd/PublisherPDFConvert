# Publisher to PDF Converter

A small Windows desktop app (C# / WinForms) that converts one or many Microsoft
Publisher (`.pub`) files to PDF. Pick your files, choose where to save, click
**Convert**.

![Windows 10 / 11](https://img.shields.io/badge/Windows-10%20%2F%2011-blue)

## What it does

- Add **one or several** `.pub` files (button, or drag-and-drop files/folders onto the window)
- Save each PDF **next to the original**, or send them all to **one output folder**
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

### Alternative: build with the .NET SDK / Visual Studio

If you have them installed:

```powershell
dotnet build -c Release
```

(or open `PublisherToPdf.csproj` in Visual Studio and press F5).

## Usage

1. Run `bin\PublisherToPdf.exe`.
2. Click **Add Files…** (or drag `.pub` files onto the window).
3. Choose the output location:
   - *Save each PDF next to its original* (default), or
   - *Save all PDFs to this folder* and browse to a folder.
4. Click **Convert**. Watch the per-file status and progress bar.

Each `document.pub` becomes `document.pdf`. Existing PDFs with the same name are
overwritten.

## Project layout

| Path | Purpose |
|------|---------|
| `src/Program.cs` | App entry point |
| `src/MainForm.cs` | The window and all UI / batch logic |
| `src/PublisherConverter.cs` | Publisher COM automation (open + export to PDF) |
| `src/app.manifest` | High-DPI awareness + Windows 10/11 compatibility |
| `build.ps1` | One-command build using the in-box `csc.exe` |
| `PublisherToPdf.csproj` | Optional project file for the .NET SDK / Visual Studio |

## Notes & troubleshooting

- **"Publisher not found"** — install Microsoft Publisher (included with some
  Microsoft 365 / Office plans), then retry.
- **A file shows "Failed"** — hover the row to see the reason (e.g. a corrupt or
  password-protected file). The batch continues with the remaining files.
- Your source files are never modified: plain conversions open the `.pub` read-only,
  and "Fit page to content" works on a temporary copy.
- Converting many large files launches Publisher once for the whole batch and
  closes it at the end, which is much faster than one-at-a-time.
- **Some text may not appear** in rare files: Publisher's own PDF engine can drop
  certain text boxes (observed with boxes whose text overflows or was pasted with
  unusual formatting). Because the app uses that same engine, such text won't appear
  in the PDF either — it would be missing from Publisher's own "Save As PDF" too.
  Re-creating the affected text box in Publisher usually resolves it.
