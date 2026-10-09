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
        // Publisher processes that existed before we started ours: never touched. Ours is the
        // one that appears after CreateInstance, so Dispose can end it if Quit() is ignored.
        private int[] _preexistingPublisherPids = new int[0];
        private int _ownPublisherPid;
        private int _startRetries;

        private static int[] PublisherPids()
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("MSPUB")) { list.Add(p.Id); p.Dispose(); }
            return list.ToArray();
        }

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
            _preexistingPublisherPids = PublisherPids();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try { _app = Activator.CreateInstance(appType); break; }
                catch (Exception ex) { last = ex; System.Threading.Thread.Sleep(1000); }
            }
            if (_app != null)
            {
                foreach (int pid in PublisherPids())
                    if (Array.IndexOf(_preexistingPublisherPids, pid) < 0) { _ownPublisherPid = pid; break; }
                // Make sure the server we got actually answers (a dying instance can be handed out).
                try { string v = ((dynamic)_app).Version; }
                catch (Exception ex)
                {
                    if (!IsPublisherGone(ex) || ++_startRetries > 3) throw;
                    Phase("fresh Publisher did not answer; starting another");
                    Dispose();
                    System.Threading.Thread.Sleep(1500);
                    Start();
                    _startRetries = 0;
                    return;
                }
            }
            if (_app == null)
            {
                throw new ConversionException(
                    "Could not start Microsoft Publisher. If a previous conversion just finished, " +
                    "wait a moment and try again.\r\n\r\nDetails: " + last.Message, last);
            }

            WarmUp();
        }

        /// <summary>
        /// Publisher's text engine finishes initialising a second or two after the
        /// automation server starts. A publication opened before that comes back
        /// half-initialised — every character reads as '#', custom text styles and
        /// stories are missing — and ExportAsFixedFormat renders that broken state
        /// (measured: a 77 KB PDF with one font instead of 172 KB with eight). The
        /// document never recovers, so the only cure is to not be first: create a
        /// throwaway blank publication, touch its text styles, and close it.
        /// </summary>
        private void WarmUp()
        {
            // Nothing to do here: the first Open is the sacrificial one (see EnsureWarm).
            // Documents.Add() would also warm the engine up, but it puts Publisher's main
            // window on screen for the seconds it takes, which Open does not.
        }

        private bool _warm;

        /// <summary>
        /// Opens and closes <paramref name="path"/> once before the first real open. The first
        /// publication opened after launch is the broken one (see the class remarks); the same
        /// file opened again is fine.
        /// </summary>
        private void EnsureWarm(string path)
        {
            if (_warm) return;
            _warm = true;
            dynamic app = _app;
            dynamic doc = null;
            try
            {
                Phase("warm-up: first Open");
                doc = app.Open(path, true);
                HideWindow();
                // TextStyles is null until the text engine is up; poll briefly.
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        dynamic styles = doc.TextStyles;
                        if (styles != null && (int)styles.Count > 0) break;
                    }
                    catch { /* not ready yet */ }
                    System.Threading.Thread.Sleep(250);
                }
                Phase("warm-up: done");
            }
            catch { /* best effort: a failed warm-up must not block conversion */ }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(); } catch { }
                    try { Marshal.ReleaseComObject(doc); } catch { }
                }
            }
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
            Retrying(() => { ConvertCore(inputPath, outputPath, fitPageToContent); return 0; });
        }

        /// <summary>
        /// Runs <paramref name="op"/>; if Publisher went away underneath it (it crashes now and
        /// then under automation, also right at start-up), starts a fresh Publisher and tries once more.
        /// </summary>
        private T Retrying<T>(Func<T> op)
        {
            try { return op(); }
            catch (Exception ex)
            {
                if (!IsPublisherGone(ex)) throw;
                Phase("Publisher went away (" + ex.Message + "); restarting");
                Restart();
                return op();
            }
        }

        private void ConvertCore(string inputPath, string outputPath, bool fitPageToContent)
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
                EnsureWarm(inputPath);
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

        /// <summary>
        /// Converts a .pub to an IDML package (editable in DesignCraft, InDesign and
        /// Affinity Publisher). Returns the exporter's warnings: shapes that had to be
        /// rasterised, pictures that could not be extracted, and so on.
        /// </summary>
        public System.Collections.Generic.List<string> ConvertToIdml(string inputPath, string outputPath, Idml.IdmlOptions options)
        {
            return ConvertToIdml(inputPath, outputPath, options, null);
        }

        /// <param name="modelDumpPath">When set, the intermediate model is also written there as JSON (debugging aid).</param>
        public System.Collections.Generic.List<string> ConvertToIdml(string inputPath, string outputPath, Idml.IdmlOptions options, string modelDumpPath)
        {
            return Retrying(() => ConvertToIdmlCore(inputPath, outputPath, options, modelDumpPath));
        }

        private System.Collections.Generic.List<string> ConvertToIdmlCore(string inputPath, string outputPath, Idml.IdmlOptions options, string modelDumpPath)
        {
            if (_app == null)
                throw new InvalidOperationException("Call Start() before converting.");
            if (!File.Exists(inputPath))
                throw new ConversionException("File not found: " + inputPath);

            string outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            string tempDir = Path.Combine(Path.GetTempPath(), "PubToIdml_" + Guid.NewGuid().ToString("N"));
            dynamic app = _app;
            dynamic doc = null;
            try
            {
                EnsureWarm(inputPath);
                Phase("idml: Open");
                doc = app.Open(inputPath, true);
                Phase("idml: opened, hiding");
                HideWindow();
                var reader = new PublisherReader(tempDir);
                Model.PubDocument model = reader.Read(app, doc);
                Phase("idml: read done");
                if (modelDumpPath != null)
                {
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
                    File.WriteAllText(modelDumpPath, js.Serialize(model));
                }
                new Idml.IdmlWriter(model, options).Write(outputPath);
                Phase("idml: written");
                return model.Warnings;
            }
            catch (ConversionException) { throw; }
            catch (Exception ex)
            {
                throw new ConversionException(
                    "Failed to convert '" + Path.GetFileName(inputPath) + "' to IDML: " + ex.Message, ex);
            }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(); } catch { }
                    try { Marshal.ReleaseComObject(doc); } catch { }
                }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
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
                HideWindow();
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
                    HideWindow();

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
                // Publisher ignores Quit() while any document/shape wrapper is alive, so release
                // everything the readers touched first, then quit, then make sure it is gone.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                try { ((dynamic)_app).Quit(); } catch { /* ignore */ }
                try { Marshal.ReleaseComObject(_app); } catch { }
                _app = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Publisher runs as more than one process; every MSPUB that was not there before we
                // started is ours. Give them a moment to honour Quit, then end the stragglers.
                var notes = new System.Collections.Generic.List<string>();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && OwnPublisherPids().Length > 0)
                    System.Threading.Thread.Sleep(250);
                foreach (int pid in OwnPublisherPids())
                {
                    try { using (var p = System.Diagnostics.Process.GetProcessById(pid)) { p.Kill(); } notes.Add(pid + " ignored Quit; killed"); }
                    catch (Exception ex) { notes.Add(pid + ": " + ex.Message); }
                }
                LastDisposeNote = notes.Count == 0 ? "Publisher exited after Quit" : "Publisher " + string.Join(", ", notes);
                _ownPublisherPid = 0;
            }
        }

        private int[] OwnPublisherPids()
        {
            var own = new System.Collections.Generic.List<int>();
            foreach (int pid in PublisherPids())
                if (Array.IndexOf(_preexistingPublisherPids, pid) < 0) own.Add(pid);
            return own.ToArray();
        }

        /// <summary>
        /// Publisher shows its main window whenever a publication is added or opened through
        /// automation (there is no Application.Visible); hide it again so batches run silently.
        /// </summary>
        private void HideWindow()
        {
            // The document window: through the object model.
            try { ((dynamic)_app).ActiveWindow.Visible = false; } catch { }
            // The application frame that Documents.Add/Open put on screen: there is no
            // Application.Visible in Publisher, so hide every top-level window of our process.
            foreach (int pid in OwnPublisherPids())
            {
                try
                {
                    EnumWindows((hwnd, lParam) =>
                    {
                        uint owner;
                        GetWindowThreadProcessId(hwnd, out owner);
                        if (owner == (uint)pid && IsWindowVisible(hwnd)) ShowWindow(hwnd, SW_HIDE);
                        return true;
                    }, IntPtr.Zero);
                }
                catch { }
            }
        }

        private const int SW_HIDE = 0;
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Did Publisher itself go away (it crashes now and then under automation)? Such errors
        /// are COM disconnects: the server died or the object is no longer valid.
        /// </summary>
        public static bool IsPublisherGone(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var com = e as COMException;
                if (com != null)
                {
                    uint hr = unchecked((uint)com.ErrorCode);
                    if (hr == 0x800706BA || hr == 0x800706BE || hr == 0x80010108 || hr == 0x80010105 || hr == 0x80004005) return true;
                }
                string m = e.Message ?? "";
                if (m.IndexOf("RPC server is unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.IndexOf("has disconnected from its clients", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.IndexOf("remote procedure call failed", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Ends the current Publisher (crashed or not) and starts a fresh one.</summary>
        public void Restart()
        {
            Dispose();
            _warm = false;
            System.Threading.Thread.Sleep(1500);   // let the old process finish dying before a new one registers
            Start();
        }

        /// <summary>What the last Dispose did about the Publisher process (diagnostics).</summary>
        public static string LastDisposeNote = "";

        /// <summary>Phase timestamps (diagnostics; written to the headless log).</summary>
        public static readonly System.Text.StringBuilder Trace = new System.Text.StringBuilder();
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static void Phase(string what) { Trace.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(what).Append("\r\n"); }
    }
}
