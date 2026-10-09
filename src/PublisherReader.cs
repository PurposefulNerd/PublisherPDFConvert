using System;
using System.Collections.Generic;
using System.IO;
using PublisherToPdf.Model;

namespace PublisherToPdf
{
    /// <summary>
    /// Walks an open Publisher document through the automation object model and
    /// builds a <see cref="PubDocument"/>. This is the C# twin of
    /// tools\Dump-PubObjectModel.ps1: same traversal, same rules, so the script's
    /// JSON dump is the oracle for what this class must produce.
    ///
    /// The document must have been opened on a warmed-up Publisher instance
    /// (see PublisherConverter.WarmUp); otherwise every character reads as '#'.
    /// </summary>
    public sealed class PublisherReader
    {
        // PbShapeType
        private const int TypeAutoShape = 1, TypeCallout = 2, TypeFreeform = 5, TypeGroup = 6, TypeLine = 9,
                          TypeLinkedPicture = 11, TypePicture = 13, TypeTextEffect = 15, TypeTextFrame = 17, TypeTable = 18;
        // MsoAutoShapeType
        private const int AutoRectangle = 1, AutoRoundedRectangle = 5, AutoOval = 9;
        private const int MaxRunScanChars = 6000;   // per-character COM calls above this: fall back to paragraph majority font

        private readonly string _pictureDir;
        private readonly PubDocument _doc = new PubDocument();
        private int _pictureCounter;

        /// <param name="pictureDir">Folder that receives pictures extracted with SaveAsPicture.</param>
        public PublisherReader(string pictureDir)
        {
            _pictureDir = pictureDir;
            Directory.CreateDirectory(pictureDir);
        }

        public PubDocument Read(dynamic app, dynamic doc)
        {
            _doc.SourcePath = Str(() => doc.FullName);
            _doc.PublisherVersion = Str(() => app.Version);

            ReadPageSetup(doc);
            ReadScheme(doc);
            ReadStyles(doc);
            ReadSections(doc);

            int i = 0;
            foreach (dynamic m in doc.MasterPages)
            {
                i++;
                _doc.Masters.Add(ReadPage(m, i, true));
            }
            foreach (dynamic p in doc.Pages)
                _doc.Pages.Add(ReadPage(p, I(() => p.PageIndex), false));

            try
            {
                foreach (dynamic sh in doc.ScratchArea.Shapes)
                    _doc.ScratchShapes.Add(ReadShape(sh, "scratch"));
            }
            catch (Exception ex) { Warn("Scratch area not read: " + ex.Message); }

            return _doc;
        }

        // ------------------------------------------------------------ document

        private void ReadPageSetup(dynamic doc)
        {
            dynamic ps = doc.PageSetup;
            _doc.PageWidth = D(() => ps.PageWidth);
            _doc.PageHeight = D(() => ps.PageHeight);
            _doc.Orientation = I(() => ps.Orientation);
            _doc.Layout = I(() => ps.PublicationLayout);
            _doc.FacingPages = _doc.Layout == 2 /* pbLayoutBook */ || B(() => doc.ViewTwoPageSpread);

            dynamic lg = doc.LayoutGuides;
            _doc.MarginLeft = D(() => lg.MarginLeft);
            _doc.MarginTop = D(() => lg.MarginTop);
            _doc.MarginRight = D(() => lg.MarginRight);
            _doc.MarginBottom = D(() => lg.MarginBottom);
            _doc.Columns = Math.Max(1, I(() => lg.Columns));
            _doc.ColumnGutter = D(() => lg.ColumnGutterWidth);
            _doc.MirrorGuides = B(() => lg.MirrorGuides);
            _doc.BaselineSpacing = D(() => lg.HorizontalBaseLineSpacing);
            _doc.BaselineOffset = D(() => lg.HorizontalBaseLineOffset);
        }

        private void ReadScheme(dynamic doc)
        {
            try
            {
                dynamic cs = doc.ColorScheme;
                _doc.SchemeName = Str(() => cs.Name);
                for (int idx = 1; idx <= 8; idx++)
                {
                    int captured = idx;
                    PubColor c = Color(() => cs.Colors(captured));
                    if (c != null) { c.Kind = PubColorKind.Rgb; _doc.SchemeColors[idx] = c; }
                }
            }
            catch (Exception ex) { Warn("Colour scheme not read: " + ex.Message); }
        }

        private void ReadStyles(dynamic doc)
        {
            foreach (dynamic ts in doc.TextStyles)
            {
                try
                {
                    var s = new PubStyle
                    {
                        Name = Str(() => ts.Name),
                        BaseStyle = Str(() => ts.BaseStyle),
                        NextStyle = Str(() => ts.NextParagraphStyle),
                        Font = Font(ts.Font),
                        Paragraph = Paragraph(ts.ParagraphFormat)
                    };
                    if (s.BaseStyle == "[no style]") s.BaseStyle = "";
                    _doc.Styles.Add(s);
                }
                catch (Exception ex) { Warn("Text style skipped: " + ex.Message); }
            }
        }

        private void ReadSections(dynamic doc)
        {
            try
            {
                foreach (dynamic sec in doc.Sections)
                {
                    _doc.Sections.Add(new PubSection
                    {
                        StartPageIndex = I(() => sec.StartPageIndex),
                        PageNumberStart = I(() => sec.PageNumberStart),
                        PageNumberFormat = I(() => sec.PageNumberFormat),
                        ContinueFromPrevious = B(() => sec.ContinueNumbersFromPreviousSection)
                    });
                }
            }
            catch (Exception ex) { Warn("Sections not read: " + ex.Message); }
        }

        // ---------------------------------------------------------------- pages

        private PubPage ReadPage(dynamic p, int index, bool isMaster)
        {
            var page = new PubPage
            {
                Id = I(() => p.PageID),
                Index = index,
                Name = Str(() => p.Name),
                PageNumber = Str(() => p.PageNumber),
                PageType = I(() => p.PageType),
                Width = D(() => p.Width),
                Height = D(() => p.Height),
                IsTwoPageMaster = B(() => p.IsTwoPageMaster),
                IgnoreMaster = B(() => p.IgnoreMaster),
                MarginLeft = _doc.MarginLeft, MarginTop = _doc.MarginTop,
                MarginRight = _doc.MarginRight, MarginBottom = _doc.MarginBottom,
                Columns = _doc.Columns, ColumnGutter = _doc.ColumnGutter
            };
            if (page.Width <= 0) page.Width = _doc.PageWidth;
            if (page.Height <= 0) page.Height = _doc.PageHeight;

            if (!isMaster)
            {
                try { page.MasterId = I(() => p.Master.PageID); page.MasterName = Str(() => p.Master.Name); } catch { }
            }
            else
            {
                // Only master pages (and the document) carry layout guides; ordinary pages report null.
                try
                {
                    dynamic lg = p.LayoutGuides;
                    double ml = D(() => lg.MarginLeft);
                    if (ml > 0 || D(() => lg.MarginTop) > 0)
                    {
                        page.MarginLeft = ml;
                        page.MarginTop = D(() => lg.MarginTop);
                        page.MarginRight = D(() => lg.MarginRight);
                        page.MarginBottom = D(() => lg.MarginBottom);
                        page.Columns = Math.Max(1, I(() => lg.Columns));
                        page.ColumnGutter = D(() => lg.ColumnGutterWidth);
                    }
                }
                catch { }
            }

            try { if (B(() => p.Background.Exists)) page.Background = Fill(p.Background.Fill); } catch { }

            string tag = (isMaster ? "master" : "page") + index;
            foreach (dynamic sh in p.Shapes)
            {
                try { page.Shapes.Add(ReadShape(sh, tag)); }
                catch (Exception ex) { Warn(tag + ": shape skipped: " + ex.Message); }
            }
            return page;
        }

        // --------------------------------------------------------------- shapes

        private PubShape ReadShape(dynamic sh, string tag)
        {
            var s = new PubShape
            {
                Id = I(() => sh.ID),
                Name = Str(() => sh.Name),
                PublisherType = I(() => sh.Type),
                AutoShapeType = IOr(() => sh.AutoShapeType, -2),
                Left = D(() => sh.GetLeft(3)),
                Top = D(() => sh.GetTop(3)),
                Width = D(() => sh.GetWidth(3)),
                Height = D(() => sh.GetHeight(3)),
                Rotation = D(() => sh.Rotation),
                FlipHorizontal = B(() => sh.HorizontalFlip),
                FlipVertical = B(() => sh.VerticalFlip),
                ZOrder = I(() => sh.ZOrderPosition),
                AltText = Str(() => sh.AlternativeText)
            };
            try { s.Fill = Fill(sh.Fill); } catch { }
            try { s.Stroke = Stroke(sh.Line); } catch { }
            try { s.Shadow = Shadow(sh.Shadow); } catch { }
            try { s.Wrap = Wrap(sh.TextWrap); } catch { }
            try { string a = Str(() => sh.Hyperlink.Address); if (a.Length > 0) s.HyperlinkAddress = a; } catch { }
            try
            {
                int n = I(() => sh.Adjustments.Count);
                if (n > 0)
                {
                    s.Adjustments = new double[n];
                    for (int i = 1; i <= n; i++) { int k = i; s.Adjustments[i - 1] = D(() => sh.Adjustments.Item(k)); }
                }
            }
            catch { }

            int type = s.PublisherType;
            bool hasTable = B(() => sh.HasTable);
            bool hasText = B(() => sh.HasTextFrame);

            if (type == TypeGroup)
            {
                s.Kind = PubShapeKind.Group;
                s.Children = new List<PubShape>();
                foreach (dynamic c in sh.GroupItems)
                {
                    try { s.Children.Add(ReadShape(c, tag)); }
                    catch (Exception ex) { Warn(tag + ": group child skipped: " + ex.Message); }
                }
                return s;
            }

            if (type == TypeTable || hasTable)
            {
                s.Kind = PubShapeKind.Table;
                s.Table = Table(sh.Table, s.Id);
                return s;
            }

            if (type == TypePicture || type == TypeLinkedPicture)
            {
                s.Kind = PubShapeKind.Picture;
                s.Picture = Picture(sh, tag);
                return s;
            }

            if (type == TypeLine)
            {
                s.Kind = PubShapeKind.Line;
                s.Nodes = Nodes(sh);
                s.PathClosed = false;
                return s;
            }

            bool needsRaster = NeedsRaster(sh, s, type, out s.RasterReason);
            if (needsRaster)
            {
                s.Kind = PubShapeKind.Raster;
                s.RasterPath = SavePicture(sh, tag, s.Id);
                Warn(tag + ": '" + s.Name + "' exported as an image (" + s.RasterReason + ")");
                return s;
            }

            if (type == TypeTextFrame)
                s.Kind = PubShapeKind.TextFrame;
            else if (type == TypeAutoShape && s.AutoShapeType == AutoRectangle)
                s.Kind = PubShapeKind.Rectangle;
            else if (type == TypeAutoShape && s.AutoShapeType == AutoRoundedRectangle)
                s.Kind = PubShapeKind.RoundedRectangle;
            else if (type == TypeAutoShape && s.AutoShapeType == AutoOval)
                s.Kind = PubShapeKind.Oval;
            else
            {
                // Freeforms, polylines, callouts and the other hundred autoshapes: take the path.
                s.Kind = PubShapeKind.Polygon;
                s.Nodes = Nodes(sh);
                if (s.Nodes.Count < 2)
                {
                    s.Kind = PubShapeKind.Raster;
                    s.RasterReason = "no path geometry available";
                    s.RasterPath = SavePicture(sh, tag, s.Id);
                    Warn(tag + ": '" + s.Name + "' exported as an image (" + s.RasterReason + ")");
                    return s;
                }
            }

            if (hasText)
            {
                // Autoshapes claim HasTextFrame but deny access until text is typed into them.
                // Line bounds are in page coordinates: useless once rotated (a flip keeps the bounds).
                try { s.TextFrame = TextFrame(sh.TextFrame, s.Id, Math.Abs(s.Rotation) < 0.01); }
                catch (System.Runtime.InteropServices.COMException) { }
                catch (UnauthorizedAccessException) { /* E_ACCESSDENIED: the autoshape has no text */ }
                catch (Exception ex) { Warn(tag + ": text of '" + s.Name + "' not read: " + ex.Message); }
            }
            return s;
        }

        private bool NeedsRaster(dynamic sh, PubShape s, int type, out string reason)
        {
            reason = null;
            switch (type)
            {
                case TypeTextEffect: reason = "WordArt"; return true;
                case 3: reason = "chart"; return true;
                case 7: case 10: case 12: reason = "OLE object"; return true;
                case 8: reason = "form control"; return true;
                case 16: reason = "media"; return true;
                case 14: reason = "placeholder"; return true;
                case 113: reason = "barcode"; return true;
            }
            if (type >= 100 && type <= 112) { reason = "web control"; return true; }
            if (s.Fill != null && s.Fill.Visible && (s.Fill.Type == 2 || s.Fill.Type == 4 || s.Fill.Type == 6))
            {
                reason = s.Fill.Type == 2 ? "pattern fill" : s.Fill.Type == 4 ? "texture fill" : "picture fill";
                return true;
            }
            try { if (B(() => sh.BorderArt.Exists)) { reason = "BorderArt"; return true; } } catch { }
            try { if (B(() => sh.ThreeD.Visible)) { reason = "3-D effect"; return true; } } catch { }
            return false;
        }

        private string SavePicture(dynamic sh, string tag, int id)
        {
            string path = Path.Combine(_pictureDir, string.Format("{0}-{1}-{2}.png", tag, id, ++_pictureCounter));
            try
            {
                sh.SaveAsPicture(path, 3 /* pbPictureResolutionCommercialPrint_300dpi */);
                if (File.Exists(path)) return path;
            }
            catch (Exception ex) { Warn(tag + ": SaveAsPicture failed for shape " + id + ": " + ex.Message); }
            return null;
        }

        private PubPicture Picture(dynamic sh, string tag)
        {
            dynamic pf = sh.PictureFormat;
            var pic = new PubPicture
            {
                OriginalName = Str(() => pf.Filename),
                Linked = B(() => pf.IsLinked),
                CropLeft = D(() => pf.CropLeft),
                CropTop = D(() => pf.CropTop),
                CropRight = D(() => pf.CropRight),
                CropBottom = D(() => pf.CropBottom),
                OriginalWidth = D(() => pf.OriginalWidth),
                OriginalHeight = D(() => pf.OriginalHeight),
                ImageFormat = I(() => pf.ImageFormat)
            };
            if (pic.Linked)
            {
                string src = Str(() => sh.LinkFormat.SourceFullName);
                if (src.Length == 0) src = pic.OriginalName;
                if (File.Exists(src)) pic.SourcePath = src;
            }
            // Always extract too: it is the only route for embedded pictures and a
            // fallback for broken links. Note SaveAsPicture applies the crop and re-encodes.
            pic.ExtractedPath = SavePicture(sh, tag, I(() => sh.ID));
            return pic;
        }

        private List<PubPathNode> Nodes(dynamic sh)
        {
            var list = new List<PubPathNode>();
            try
            {
                dynamic nodes = sh.Nodes;
                int n = I(() => nodes.Count);
                for (int i = 1; i <= n; i++)
                {
                    dynamic nd = nodes.Item(i);
                    Array pts = (Array)nd.Points;
                    int lb0 = pts.GetLowerBound(0), lb1 = pts.GetLowerBound(1);
                    list.Add(new PubPathNode
                    {
                        X = Convert.ToDouble(pts.GetValue(lb0, lb1)),
                        Y = Convert.ToDouble(pts.GetValue(lb0, lb1 + 1)),
                        SegmentType = I(() => nd.SegmentType),
                        EditingType = I(() => nd.EditingType)
                    });
                }
                // Publisher lists Bézier segments as three nodes: two control points then the anchor.
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].SegmentType == 1 && i >= 2)
                    {
                        list[i - 1].IsControlPoint = true;
                        list[i - 2].IsControlPoint = true;
                    }
                }
            }
            catch { }
            if (list.Count >= 2) return list;

            // No node list (most autoshapes): fall back to the polygon approximation in Vertices.
            list.Clear();
            try
            {
                Array v = (Array)sh.Vertices;
                int lb0 = v.GetLowerBound(0), lb1 = v.GetLowerBound(1);
                for (int i = 0; i < v.GetLength(0); i++)
                {
                    list.Add(new PubPathNode
                    {
                        X = Convert.ToDouble(v.GetValue(lb0 + i, lb1)),
                        Y = Convert.ToDouble(v.GetValue(lb0 + i, lb1 + 1))
                    });
                }
            }
            catch { }
            return list;
        }

        private PubTable Table(dynamic t, int shapeId)
        {
            var tbl = new PubTable { Rows = I(() => t.Rows.Count), Columns = I(() => t.Columns.Count) };
            foreach (dynamic c in t.Columns) tbl.ColumnWidths.Add(D(() => c.Width));
            foreach (dynamic r in t.Rows) tbl.RowHeights.Add(D(() => r.Height));

            // Merged cells report their span in Width/Height and are repeated at every
            // position they cover (Cell.Row/Column are not reliable for them), so walk
            // row-major and mark covered positions: the first uncovered position is the origin.
            var covered = new bool[tbl.Rows + 1, tbl.Columns + 1];
            for (int r = 1; r <= tbl.Rows; r++)
            {
                for (int c = 1; c <= tbl.Columns; c++)
                {
                    if (covered[r, c]) continue;
                    int rr = r, cc = c;
                    dynamic cell = t.Cells(rr, cc, rr, cc).Item(1);
                    int span = Math.Max(1, I(() => cell.Width)), rows = Math.Max(1, I(() => cell.Height));
                    int originR = r, originC = c;
                    for (int dr = 0; dr < rows && r + dr <= tbl.Rows; dr++)
                        for (int dc = 0; dc < span && c + dc <= tbl.Columns; dc++)
                            covered[r + dr, c + dc] = true;

                    var pc = new PubCell
                    {
                        Row = originR, Column = originC, RowSpan = rows, ColumnSpan = span,
                        VerticalAlignment = I(() => cell.VerticalTextAlignment),
                        MarginLeft = D(() => cell.MarginLeft), MarginTop = D(() => cell.MarginTop),
                        MarginRight = D(() => cell.MarginRight), MarginBottom = D(() => cell.MarginBottom)
                    };
                    try { if (B(() => cell.Fill.Visible)) pc.Fill = Color(() => cell.Fill.ForeColor); } catch { }
                    try
                    {
                        pc.BorderTop = D(() => cell.BorderTop.Weight);
                        pc.BorderBottom = D(() => cell.BorderBottom.Weight);
                        pc.BorderLeft = D(() => cell.BorderLeft.Weight);
                        pc.BorderRight = D(() => cell.BorderRight.Weight);
                        pc.BorderColor = Color(() => cell.BorderTop.Color);
                    }
                    catch { }
                    pc.Width = 0; pc.Height = 0;
                    for (int k = 0; k < span && originC - 1 + k < tbl.ColumnWidths.Count; k++) pc.Width += tbl.ColumnWidths[originC - 1 + k];
                    for (int k = 0; k < rows && originR - 1 + k < tbl.RowHeights.Count; k++) pc.Height += tbl.RowHeights[originR - 1 + k];

                    int storyId = unchecked(shapeId * 1000 + originR * 100 + originC);
                    pc.Story = Story(cell.TextRange, storyId);
                    tbl.Cells.Add(pc);
                }
            }
            return tbl;
        }

        // ----------------------------------------------------------------- text

        private PubTextFrame TextFrame(dynamic tf, int shapeId)
        {
            return TextFrame(tf, shapeId, true);
        }

        /// <param name="measure">false for rotated shapes: Publisher reports line bounds in page coordinates, so they are meaningless there.</param>
        private PubTextFrame TextFrame(dynamic tf, int shapeId, bool measure)
        {
            var f = new PubTextFrame
            {
                Columns = Math.Max(1, I(() => tf.Columns)),
                ColumnSpacing = D(() => tf.ColumnSpacing),
                MarginLeft = D(() => tf.MarginLeft),
                MarginTop = D(() => tf.MarginTop),
                MarginRight = D(() => tf.MarginRight),
                MarginBottom = D(() => tf.MarginBottom),
                VerticalAlignment = I(() => tf.VerticalTextAlignment),
                Orientation = I(() => tf.Orientation)
            };
            bool hasNext = B(() => tf.HasNextLink), hasPrev = B(() => tf.HasPreviousLink);
            if (hasNext) f.NextShapeId = IOr(() => tf.NextLinkedTextFrame.Parent.ID, 0);
            if (hasPrev) f.PreviousShapeId = IOr(() => tf.PreviousLinkedTextFrame.Parent.ID, 0);
            f.IsHead = !hasPrev;

            if (f.IsHead)
            {
                // The story lives on its head frame. Walk the thread for the frame order.
                PubStory story = Story(tf.Story.TextRange, shapeId, measure);
                story.FrameIds.Add(shapeId);
                try
                {
                    dynamic cur = tf;
                    int guard = 0;
                    while (B(() => cur.HasNextLink) && guard++ < 10000)
                    {
                        cur = cur.NextLinkedTextFrame;
                        story.FrameIds.Add(I(() => cur.Parent.ID));
                    }
                }
                catch { }
                _doc.Stories[shapeId] = story;
                f.StoryId = shapeId;
            }
            else
            {
                // Resolved later by the writer: find the story whose FrameIds contains this frame.
                f.StoryId = 0;
            }
            return f;
        }

        private PubStory Story(dynamic tr, int id)
        {
            return Story(tr, id, true);
        }

        private PubStory Story(dynamic tr, int id, bool measure)
        {
            var st = new PubStory { Id = id, Text = Str(() => tr.Text), TextBoundHeight = measure ? D(() => tr.BoundHeight) : 0 };
            // Publisher ends the text with a trailing paragraph mark; IDML stories don't want it.
            if (st.Text.EndsWith("\r")) st.Text = st.Text.Substring(0, st.Text.Length - 1);
            int len = st.Text.Length;

            int pc = IOr(() => tr.ParagraphsCount, 1);
            for (int i = 1; i <= pc; i++)
            {
                int k = i;
                try
                {
                    dynamic p = tr.Paragraphs(k, 1);
                    var para = new PubParagraph
                    {
                        Start = I(() => p.Start),
                        End = I(() => p.End),
                        Format = Paragraph(p.ParagraphFormat)
                    };
                    // Publisher's line-spacing unit ("sp") is the font's own line height, which we
                    // cannot know here; take the pitch Publisher actually laid out instead.
                    try
                    {
                        int lines = measure ? I(() => p.LinesCount) : 0;
                        if (lines >= 2)
                            para.Format.MeasuredLeading = D(() => p.Lines(2, 1).BoundTop) - D(() => p.Lines(1, 1).BoundTop);
                        else if (lines == 1)
                            para.Format.MeasuredLeading = D(() => p.Lines(1, 1).BoundHeight) - para.Format.SpaceAfter - para.Format.SpaceBefore;
                        if (para.Format.MeasuredLeading < 1) para.Format.MeasuredLeading = 0;
                    }
                    catch { }
                    try
                    {
                        dynamic dc = p.DropCap;
                        int lines = IOr(() => dc.LinesUp, 0);
                        if (lines > 0) { para.Format.DropCapLines = lines; para.Format.DropCapSpan = Math.Max(1, IOr(() => dc.Span, 1)); }
                    }
                    catch { }
                    st.Paragraphs.Add(para);
                }
                catch (Exception ex) { Warn("Paragraph " + k + " of story " + id + " skipped: " + ex.Message); }
            }
            // Publisher's Start/End are offsets into the *containing* story: for a table cell or
            // a sub-range they do not start at 0. Rebase on the first paragraph, then trust the
            // text itself for the boundaries (one paragraph per '\r') and keep the formats in order.
            int baseOffset = st.Paragraphs.Count > 0 ? st.Paragraphs[0].Start : 0;
            var bounds = new List<int>();
            for (int i = 0; i < st.Text.Length; i++) if (st.Text[i] == '\r') bounds.Add(i + 1);
            var rebuilt = new List<PubParagraph>();
            int from = 0;
            for (int i = 0; i <= bounds.Count; i++)
            {
                int to = i < bounds.Count ? bounds[i] : st.Text.Length;
                PubParagraphFormat fmt = i < st.Paragraphs.Count ? st.Paragraphs[i].Format
                                       : st.Paragraphs.Count > 0 ? st.Paragraphs[st.Paragraphs.Count - 1].Format
                                       : new PubParagraphFormat { StyleName = "Normal", ListType = 255 };
                rebuilt.Add(new PubParagraph { Start = from, End = to, Format = fmt });
                from = to;
            }
            st.Paragraphs = rebuilt;

            // Character runs: one COM call per character, collapsed while the formatting is equal.
            if (len > 0 && len <= MaxRunScanChars)
            {
                string curKey = null; PubFont curFont = null; int runStart = 0;
                for (int i = 1; i <= len; i++)
                {
                    int k = i;
                    PubFont f;
                    try { f = Font(tr.Characters(k, 1).Font); } catch { continue; }
                    string key = f.Key();
                    if (key != curKey)
                    {
                        if (curFont != null) st.Runs.Add(new PubRun { Start = runStart, Length = i - 1 - runStart, Font = curFont });
                        curKey = key; curFont = f; runStart = i - 1;
                    }
                }
                if (curFont != null) st.Runs.Add(new PubRun { Start = runStart, Length = len - runStart, Font = curFont });
            }
            else if (len > 0)
            {
                Warn("Story " + id + " is long (" + len + " chars); character formatting taken per paragraph.");
                foreach (var para in st.Paragraphs)
                {
                    int a = para.Start, b = Math.Min(para.End, len);
                    if (b <= a) continue;
                    PubFont f = null;
                    try { f = Font(tr.Characters(a + 1, b - a).MajorityFont); } catch { }
                    if (f != null) st.Runs.Add(new PubRun { Start = a, Length = b - a, Font = f });
                }
            }

            try
            {
                int hc = I(() => tr.Hyperlinks.Count);
                for (int i = 1; i <= hc; i++)
                {
                    int k = i;
                    dynamic h = tr.Hyperlinks.Item(k);
                    st.Hyperlinks.Add(new PubHyperlink { Start = I(() => h.Range.Start) - baseOffset, End = I(() => h.Range.End) - baseOffset, Address = Str(() => h.Address) });
                }
            }
            catch { }
            try
            {
                int fc = I(() => tr.Fields.Count);
                for (int i = 1; i <= fc; i++)
                {
                    int k = i;
                    dynamic fl = tr.Fields.Item(k);
                    int start = I(() => fl.TextRange.Start) - baseOffset;
                    int end = IOr(() => fl.TextRange.End, start + baseOffset + 1) - baseOffset;
                    st.Fields.Add(new PubField { Start = start, Length = Math.Max(1, end - start), Type = I(() => fl.Type), Result = Str(() => fl.Result) });
                }
            }
            catch { }
            return st;
        }

        private PubFont Font(dynamic f)
        {
            return new PubFont
            {
                Name = Str(() => f.Name),
                Size = D(() => f.Size),
                Bold = B(() => f.Bold),
                Italic = B(() => f.Italic),
                Underline = IOr(() => f.Underline, 0),
                Color = Color(() => f.Color),
                AllCaps = B(() => f.AllCaps),
                SmallCaps = B(() => f.SmallCaps),
                StrikeThrough = B(() => f.StrikeThrough),
                Superscript = B(() => f.SuperScript),
                Subscript = B(() => f.SubScript),
                Tracking = DOr(() => f.Tracking, 100),
                Kerning = DOr(() => f.Kerning, 0),
                Scaling = DOr(() => f.Scaling, 100),
                Position = DOr(() => f.Position, 0)
            };
        }

        private PubParagraphFormat Paragraph(dynamic pf)
        {
            var p = new PubParagraphFormat
            {
                StyleName = Str(() => pf.TextStyle),
                Alignment = IOr(() => pf.Alignment, 0),
                LeftIndent = D(() => pf.LeftIndent),
                RightIndent = D(() => pf.RightIndent),
                FirstLineIndent = D(() => pf.FirstLineIndent),
                SpaceBefore = D(() => pf.SpaceBefore),
                SpaceAfter = D(() => pf.SpaceAfter),
                LineSpacing = DOr(() => pf.LineSpacing, 1),
                LineSpacingRule = IOr(() => pf.LineSpacingRule, 0),
                ListType = IOr(() => pf.ListType, 255),
                BulletText = Str(() => pf.ListBulletText),
                ListIndent = D(() => pf.ListIndent),
                ListNumberStart = IOr(() => pf.ListNumberStart, 1),
                KeepLinesTogether = B(() => pf.KeepLinesTogether),
                KeepWithNext = B(() => pf.KeepWithNext),
                WidowControl = B(() => pf.WidowControl)
            };
            try
            {
                int n = I(() => pf.Tabs.Count);
                for (int i = 1; i <= n; i++)
                {
                    int k = i;
                    dynamic t = pf.Tabs.Item(k);
                    double pos = D(() => t.Position);
                    if (pos > 0) p.Tabs.Add(new PubTabStop { Position = pos, Alignment = IOr(() => t.Alignment, 0), Leader = IOr(() => t.Leader, 0) });
                }
            }
            catch { }
            return p;
        }

        // -------------------------------------------------------------- graphics

        private PubFill Fill(dynamic fl)
        {
            var f = new PubFill
            {
                Visible = B(() => fl.Visible),
                Type = IOr(() => fl.Type, 1),
                Fore = Color(() => fl.ForeColor),
                Back = Color(() => fl.BackColor),
                Transparency = DOr(() => fl.Transparency, 0)
            };
            if (f.Type == 3)
            {
                f.GradientStyle = IOr(() => fl.GradientStyle, 1);
                f.GradientVariant = IOr(() => fl.GradientVariant, 1);
                f.GradientAngle = DOr(() => fl.GradientAngle, 0);
                f.GradientColorType = IOr(() => fl.GradientColorType, 2);
            }
            return f;
        }

        private PubStroke Stroke(dynamic ln)
        {
            return new PubStroke
            {
                Visible = B(() => ln.Visible),
                Weight = DOr(() => ln.Weight, 0),
                Color = Color(() => ln.ForeColor),
                DashStyle = IOr(() => ln.DashStyle, 1),
                LineStyle = IOr(() => ln.Style, 1),
                CapStyle = IOr(() => ln.CapStyle, 3),
                JoinStyle = IOr(() => ln.JoinStyle, 3),
                BeginArrow = IOr(() => ln.BeginArrowheadStyle, 1),
                EndArrow = IOr(() => ln.EndArrowheadStyle, 1),
                Transparency = DOr(() => ln.Transparency, 0)
            };
        }

        private PubShadow Shadow(dynamic sh)
        {
            var s = new PubShadow { Visible = B(() => sh.Visible) };
            if (!s.Visible) return s;
            s.Blur = DOr(() => sh.Blur, 0);
            s.OffsetX = DOr(() => sh.OffsetX, 0);
            s.OffsetY = DOr(() => sh.OffsetY, 0);
            s.Color = Color(() => sh.ForeColor);
            s.Transparency = DOr(() => sh.Transparency, 0);
            return s;
        }

        private PubWrap Wrap(dynamic w)
        {
            return new PubWrap
            {
                Type = IOr(() => w.Type, 0),
                Side = IOr(() => w.Side, 0),
                DistanceAuto = B(() => w.DistanceAuto),
                Left = DOr(() => w.DistanceLeft, 0),
                Top = DOr(() => w.DistanceTop, 0),
                Right = DOr(() => w.DistanceRight, 0),
                Bottom = DOr(() => w.DistanceBottom, 0)
            };
        }

        private static PubColor Color(Func<dynamic> get)
        {
            try
            {
                dynamic c = get();
                if (c == null) return null;
                int bgr = Convert.ToInt32(c.RGB);
                var col = new PubColor { R = bgr & 0xFF, G = (bgr >> 8) & 0xFF, B = (bgr >> 16) & 0xFF };
                int type = 1;
                try { type = Convert.ToInt32(c.Type); } catch { }
                if (type == 2) { col.Kind = PubColorKind.Scheme; col.SchemeIndex = Convert.ToInt32(c.SchemeColor); }
                else if (type == 3)
                {
                    col.Kind = PubColorKind.Cmyk;
                    dynamic k = c.CMYK;
                    col.C = Convert.ToInt32(k.Cyan); col.M = Convert.ToInt32(k.Magenta); col.Y = Convert.ToInt32(k.Yellow); col.K = Convert.ToInt32(k.Black);
                }
                else col.Kind = PubColorKind.Rgb;
                try { col.Tint = Convert.ToDouble(c.TintAndShade); } catch { }
                try { col.Transparency = Convert.ToDouble(c.Transparency); } catch { }
                return col;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------- helpers

        private void Warn(string msg) { _doc.Warnings.Add(msg); }

        private static string Str(Func<dynamic> get)
        {
            try { object v = get(); return v == null ? "" : Convert.ToString(v); } catch { return ""; }
        }
        private static double D(Func<dynamic> get) { return DOr(get, 0); }
        private static double DOr(Func<dynamic> get, double fallback)
        {
            try { object v = get(); return v == null ? fallback : Convert.ToDouble(v); } catch { return fallback; }
        }
        private static int I(Func<dynamic> get) { return IOr(get, 0); }
        private static int IOr(Func<dynamic> get, int fallback)
        {
            try { object v = get(); return v == null ? fallback : Convert.ToInt32(v); } catch { return fallback; }
        }
        /// <summary>MsoTriState / Boolean → bool (msoTrue is -1).</summary>
        private static bool B(Func<dynamic> get)
        {
            try
            {
                object v = get();
                if (v == null) return false;
                if (v is bool) return (bool)v;
                return Convert.ToInt32(v) != 0;
            }
            catch { return false; }
        }
    }
}
