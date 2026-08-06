using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PublisherToPdf
{
    /// <summary>
    /// Raised when conversion cannot proceed or fails for a specific file.
    /// </summary>
    public sealed class ConversionException : Exception
    {
        public ConversionException(string message) : base(message) { }
        public ConversionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Drives Microsoft Publisher through late-bound COM automation to export
    /// .pub files to PDF. A single Publisher application instance is reused for
    /// an entire batch, which is much faster than launching it per file.
    ///
    /// Requires Microsoft Publisher to be installed. There is no faithful open
    /// alternative for the proprietary .pub format, so Publisher does the render.
    /// </summary>
    public sealed class PublisherConverter : IDisposable
    {
        // PbFixedFormatType.pbFixedFormatTypePDF
        private const int PdfFormat = 2;
        // PbFixedFormatIntent.pbIntentStandard (good quality, screen + print)
        private const int IntentStandard = 2;
        // 1" margin added around content when fitting the page. It also absorbs
        // the small amount by which Publisher under-reports the bounds of rotated
        // shapes, so nothing ends up clipped at the page edge.
        private const double FitMarginPts = 72;

        private object _app;

        /// <summary>Returns true when the Publisher COM server is registered on this machine.</summary>
        public static bool IsPublisherInstalled
        {
            get { return Type.GetTypeFromProgID("Publisher.Application") != null; }
        }

        /// <summary>Launches (or attaches to) the Publisher automation server.</summary>
        public void Start()
        {
            Type appType = Type.GetTypeFromProgID("Publisher.Application");
            if (appType == null)
            {
                throw new ConversionException(
                    "Microsoft Publisher is not installed on this computer.\r\n\r\n" +
                    "Publisher is required to open and convert .pub files. Please install " +
                    "Microsoft Publisher (part of some Microsoft 365 / Office plans) and try again.");
            }

            // Launching Publisher can briefly fail with "RPC server is unavailable"
            // when a previous instance is still shutting down (e.g. running a second
            // batch right after the first). Retry a few times before giving up.
            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try { _app = Activator.CreateInstance(appType); return; }
                catch (Exception ex) { last = ex; System.Threading.Thread.Sleep(1000); }
            }
            throw new ConversionException(
                "Could not start Microsoft Publisher. If a previous conversion just finished, " +
                "wait a moment and try again.\r\n\r\nDetails: " + last.Message, last);
        }

        /// <summary>
        /// Converts a single .pub file to a PDF at <paramref name="outputPath"/>.
        /// Any existing file at the output path is overwritten.
        /// </summary>
        /// <param name="fitPageToContent">
        /// When true, and the publication's content extends past the defined page
        /// edges (common when items sit on Publisher's scratch area), the page is
        /// grown to enclose all content so nothing is clipped. The source file is
        /// never modified — fitting happens on a throwaway copy.
        /// </param>
        public void Convert(string inputPath, string outputPath, bool fitPageToContent)
        {
            if (_app == null)
                throw new InvalidOperationException("Call Start() before converting.");

            if (!File.Exists(inputPath))
                throw new ConversionException("File not found: " + inputPath);

            string outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            try
            {
                if (fitPageToContent && TryConvertFitted(inputPath, outputPath))
                    return;

                ExportReadOnly(inputPath, outputPath);
            }
            catch (ConversionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConversionException(
                    "Failed to convert '" + Path.GetFileName(inputPath) + "': " + ex.Message, ex);
            }
        }

        /// <summary>Opens the file read-only and exports it exactly as its page is defined.</summary>
        private void ExportReadOnly(string inputPath, string outputPath)
        {
            dynamic app = _app;
            dynamic doc = null;
            try
            {
                // Open(Filename, [ReadOnly]). Passing the later optional args
                // (AddToRecentFiles/PromptUser) is rejected by some Publisher
                // builds ("value does not fall within the expected range"),
                // so we open read-only with just the two parameters.
                doc = app.Open(inputPath, true);
                doc.ExportAsFixedFormat(PdfFormat, outputPath, IntentStandard);
            }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(); } catch { /* ignore close errors */ }
                    try { Marshal.ReleaseComObject(doc); } catch { }
                }
            }
        }

        /// <summary>
        /// If the single-page publication has content spilling past the page edges,
        /// exports a page grown to contain everything. Returns false (so the caller
        /// falls back to a plain export) when there is no overflow or the layout
        /// cannot be adjusted safely.
        /// </summary>
        private bool TryConvertFitted(string inputPath, string outputPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PubToPdf_" + Guid.NewGuid().ToString("N"));
            string tempPub = Path.Combine(tempDir, Path.GetFileName(inputPath));
            try
            {
                Directory.CreateDirectory(tempDir);
                File.Copy(inputPath, tempPub, true);

                dynamic app = _app;
                dynamic doc = null;
                try
                {
                    doc = app.Open(tempPub, false); // writable copy — original is untouched

                    // Fitting a single-page flyer is safe; multi-page publications
                    // share one page size, so leave those to the plain export.
                    if ((int)doc.Pages.Count != 1)
                        return false;

                    dynamic ps = doc.PageSetup;
                    double pw = (double)ps.PageWidth, ph = (double)ps.PageHeight;
                    dynamic page = doc.Pages.Item(1);
                    int count = (int)page.Shapes.Count;
                    if (count < 2)
                        return false;

                    double minX, minY, maxX, maxY;
                    MeasureContentBounds(page, out minX, out minY, out maxX, out maxY);

                    bool overflow = minX < -0.5 || minY < -0.5 || maxX > pw + 0.5 || maxY > ph + 0.5;
                    if (!overflow)
                        return false;

                    // Group so Publisher reports an accurate, rotation-aware bounding
                    // box, move it into the margin, then size the page to fit.
                    int[] idx = new int[count];
                    for (int i = 0; i < count; i++) idx[i] = i + 1;
                    dynamic grp = page.Shapes.Range(idx).Group();
                    double gw = (double)grp.Width, gh = (double)grp.Height;
                    ps.PageWidth = gw + 2 * FitMarginPts;
                    ps.PageHeight = gh + 2 * FitMarginPts;
                    grp.Left = FitMarginPts;
                    grp.Top = FitMarginPts;
                    try { grp.Ungroup(); } catch { /* leaving it grouped is fine for export */ }

                    doc.ExportAsFixedFormat(PdfFormat, outputPath, IntentStandard);
                    try { doc.Saved = true; } catch { }
                    return true;
                }
                finally
                {
                    if (doc != null)
                    {
                        try { doc.Saved = true; } catch { }
                        try { doc.Close(); } catch { }
                        try { Marshal.ReleaseComObject(doc); } catch { }
                    }
                }
            }
            catch
            {
                // Any trouble adjusting the layout: fall back to a faithful plain export.
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Rotation-aware bounding box of every shape on the page, in points.
        /// Used only to decide whether content overflows the page.
        /// </summary>
        private static void MeasureContentBounds(dynamic page, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.MaxValue; minY = double.MaxValue;
            maxX = double.MinValue; maxY = double.MinValue;
            foreach (dynamic sh in page.Shapes)
            {
                double l, tp, w, h, rot;
                try
                {
                    l = (double)sh.Left; tp = (double)sh.Top;
                    w = (double)sh.Width; h = (double)sh.Height;
                    rot = (double)sh.Rotation;
                }
                catch { continue; }

                double cx = l + w / 2, cy = tp + h / 2;
                double rad = rot * Math.PI / 180.0;
                double cos = Math.Cos(rad), sin = Math.Sin(rad);
                double[] xs = { -w / 2, w / 2, w / 2, -w / 2 };
                double[] ys = { -h / 2, -h / 2, h / 2, h / 2 };
                for (int i = 0; i < 4; i++)
                {
                    double x = cx + xs[i] * cos - ys[i] * sin;
                    double y = cy + xs[i] * sin + ys[i] * cos;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        public void Dispose()
        {
            if (_app != null)
            {
                try { ((dynamic)_app).Quit(); } catch { /* ignore */ }
                try { Marshal.ReleaseComObject(_app); } catch { }
                _app = null;

                // Release the underlying RCWs so Publisher actually exits.
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
