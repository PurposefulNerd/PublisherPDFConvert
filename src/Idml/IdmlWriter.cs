using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using PublisherToPdf.Model;

namespace PublisherToPdf.Idml
{
    public sealed class IdmlOptions
    {
        /// <summary>
        /// true: every table becomes one text frame per cell plus its fills/borders
        /// (what DesignCraft can show today). false: a real IDML Table inside a story
        /// (InDesign / Affinity; DesignCraft ignores tables for now).
        /// </summary>
        public bool TablesAsFrames = true;
    }

    /// <summary>
    /// Writes a <see cref="PubDocument"/> as an IDML package (InDesign Markup
    /// Language, Adobe's public interchange format). Written from the IDML
    /// specification and checked against DesignCraft's importer (crates/idml).
    ///
    /// Coordinates: IDML spread space has its origin at the spread centre (non
    /// facing) or at the spine with y centred on the page (facing). Every page
    /// item carries an ItemTransform from its own inner path coordinates to
    /// spread space; we keep inner coordinates at the shape's top-left so the
    /// transform is translate(page) · translate(centre) · rotate · flip · translate(-centre).
    /// </summary>
    public sealed class IdmlWriter
    {
        private const string NsPkg = "http://ns.adobe.com/AdobeInDesign/idml/1.0/packaging";
        private const string Mimetype = "application/vnd.adobe.indesign-idml-package";
        private const string NoParaStyle = "ParagraphStyle/$ID/[No paragraph style]";
        private const string NoCharStyle = "CharacterStyle/$ID/[No character style]";
        private const string LayerId = "ulayer1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly PubDocument _doc;
        private readonly IdmlOptions _opt;
        private readonly Dictionary<string, Swatch> _swatches = new Dictionary<string, Swatch>();
        private readonly List<Swatch> _swatchOrder = new List<Swatch>();
        private readonly Dictionary<string, PubStyle> _styleByName = new Dictionary<string, PubStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, FrameLink> _frameLinks = new Dictionary<int, FrameLink>();
        private readonly List<StoryOut> _stories = new List<StoryOut>();
        private readonly List<HyperlinkOut> _hyperlinks = new List<HyperlinkOut>();
        private readonly HashSet<string> _fonts = new HashSet<string>();
        private readonly List<SpreadOut> _spreads = new List<SpreadOut>();
        private int _nextId = 1;

        private sealed class Swatch { public string Self, Name, Space, Value, Model = "Process"; public bool IsGradient; public string Stop0, Stop1; public bool Radial; }
        private sealed class FrameLink { public int StoryId, Prev, Next; }
        private sealed class StoryOut { public string Self; public PubStory Story; public PubTable Table; }
        private sealed class HyperlinkOut { public string SourceSelf, DestSelf, Name, Url; public int Key; }
        private sealed class SpreadOut { public string Self; public List<PageOut> Pages = new List<PageOut>(); public bool IsMaster; public PubPage Master; public double Ox, Oy; }
        private sealed class PageOut { public PubPage Page; public double X; public bool Left; }
        private sealed class WrapBox { public double X0, Y0, X1, Y1; public int Z; }

        // Publisher only wraps text that lies *beneath* a wrapped graphic; InDesign and DesignCraft
        // wrap every frame the graphic touches. So each text frame gets IgnoreWrap unless a wrapped
        // graphic stacked above it overlaps it. Collected per page while writing the spread.
        private List<WrapBox> _wrapBoxes = new List<WrapBox>();
        private int _currentTopZ;

        public IdmlWriter(PubDocument doc, IdmlOptions options)
        {
            _doc = doc;
            _opt = options ?? new IdmlOptions();
        }

        public void Write(string outputPath)
        {
            Prepare();
            if (File.Exists(outputPath)) File.Delete(outputPath);
            using (var zip = new ZipArchive(File.Create(outputPath), ZipArchiveMode.Create))
            {
                // The mimetype must be the first entry and stored, not deflated.
                var mt = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var s = mt.Open()) { byte[] b = Encoding.ASCII.GetBytes(Mimetype); s.Write(b, 0, b.Length); }

                AddPart(zip, "META-INF/container.xml", WriteContainer);
                AddPart(zip, "designmap.xml", WriteDesignMap);
                AddPart(zip, "Resources/Graphic.xml", w => Part(w, "Graphic", WriteGraphic));
                AddPart(zip, "Resources/Fonts.xml", w => Part(w, "Fonts", WriteFonts));
                AddPart(zip, "Resources/Styles.xml", w => Part(w, "Styles", WriteStyles));
                AddPart(zip, "Resources/Preferences.xml", w => Part(w, "Preferences", WritePreferences));
                foreach (var sp in _spreads)
                {
                    SpreadOut cur = sp;
                    if (sp.IsMaster) AddPart(zip, "MasterSpreads/MasterSpread_" + sp.Self + ".xml", w => Part(w, "MasterSpread", x => WriteSpread(x, cur)));
                    else AddPart(zip, "Spreads/Spread_" + sp.Self + ".xml", w => Part(w, "Spread", x => WriteSpread(x, cur)));
                }
                foreach (var st in _stories)
                {
                    StoryOut cur = st;
                    AddPart(zip, "Stories/Story_" + st.Self + ".xml", w => Part(w, "Story", x => WriteStory(x, cur)));
                }
            }
        }

        // ============================================================ preparation

        private void Prepare()
        {
            foreach (var s in _doc.Styles) if (!_styleByName.ContainsKey(s.Name)) _styleByName[s.Name] = s;

            // Frame → story resolution (continuation frames don't know their story id).
            foreach (var kv in _doc.Stories)
            {
                for (int i = 0; i < kv.Value.FrameIds.Count; i++)
                {
                    _frameLinks[kv.Value.FrameIds[i]] = new FrameLink
                    {
                        StoryId = kv.Key,
                        Prev = i > 0 ? kv.Value.FrameIds[i - 1] : 0,
                        Next = i + 1 < kv.Value.FrameIds.Count ? kv.Value.FrameIds[i + 1] : 0
                    };
                }
                _stories.Add(new StoryOut { Self = "us" + kv.Key, Story = kv.Value });
            }

            // Masters: one MasterSpread each.
            foreach (var m in _doc.Masters)
            {
                var sp = new SpreadOut { Self = "um" + m.Id, IsMaster = true, Master = m };
                if (m.IsTwoPageMaster && _doc.FacingPages)
                {
                    sp.Pages.Add(new PageOut { Page = m, X = -m.Width, Left = true });
                    sp.Pages.Add(new PageOut { Page = m, X = 0 });
                }
                else sp.Pages.Add(new PageOut { Page = m, X = 0 });
                SetOrigin(sp);
                _spreads.Add(sp);
            }

            // Document spreads: pairs when facing, else one page each.
            SpreadOut open = null;
            foreach (var p in _doc.Pages)
            {
                bool left = _doc.FacingPages && p.PageType == 1;
                if (_doc.FacingPages && open != null && !left && open.Pages.Count == 1 && open.Pages[0].Left)
                {
                    open.Pages.Add(new PageOut { Page = p, X = 0 });
                    SetOrigin(open);
                    open = null;
                    continue;
                }
                var sp = new SpreadOut { Self = "usp" + p.Id };
                sp.Pages.Add(new PageOut { Page = p, X = left ? -p.Width : 0, Left = left });
                SetOrigin(sp);
                _spreads.Add(sp);
                open = left ? sp : null;
            }

            // Collect swatches and fonts by walking everything once.
            foreach (var st in _doc.Styles) { UseFont(st.Font); }
            foreach (var sp in _spreads) foreach (var pg in sp.Pages) { if (pg.Page.Background != null) UseFill(pg.Page.Background); foreach (var sh in pg.Page.Shapes) CollectShape(sh); }
            foreach (var sh in _doc.ScratchShapes) CollectShape(sh);
            foreach (var kv in _doc.Stories) CollectStory(kv.Value);
        }

        private void SetOrigin(SpreadOut sp)
        {
            double maxH = 0, minX = double.MaxValue, maxX = double.MinValue;
            foreach (var pg in sp.Pages)
            {
                maxH = Math.Max(maxH, pg.Page.Height);
                minX = Math.Min(minX, pg.X);
                maxX = Math.Max(maxX, pg.X + pg.Page.Width);
            }
            sp.Oy = maxH / 2;
            sp.Ox = _doc.FacingPages ? 0 : (minX + maxX) / 2;
        }

        private void CollectShape(PubShape sh)
        {
            if (sh.Fill != null) UseFill(sh.Fill);
            if (sh.Stroke != null && sh.Stroke.Visible) UseColor(sh.Stroke.Color);
            if (sh.Shadow != null && sh.Shadow.Visible) UseColor(sh.Shadow.Color);
            if (sh.Children != null) foreach (var c in sh.Children) CollectShape(c);
            if (sh.Table != null)
            {
                foreach (var c in sh.Table.Cells)
                {
                    UseColor(c.Fill); UseColor(c.BorderColor);
                    if (c.Story != null) { CollectStory(c.Story); }
                }
                if (_opt.TablesAsFrames)
                {
                    foreach (var c in sh.Table.Cells) if (c.Story != null) _stories.Add(new StoryOut { Self = "us" + c.Story.Id, Story = c.Story });
                }
                else _stories.Add(new StoryOut { Self = "ust" + sh.Id, Table = sh.Table });
            }
        }

        private void CollectStory(PubStory st)
        {
            foreach (var r in st.Runs) UseFont(r.Font);
        }

        private void UseFont(PubFont f)
        {
            if (f == null) return;
            if (!string.IsNullOrEmpty(f.Name)) _fonts.Add(f.Name);
            UseColor(f.Color);
        }

        private void UseFill(PubFill f)
        {
            if (f == null || !f.Visible) return;
            if (f.Type == 3) GradientSwatch(f);
            else UseColor(f.Fore);
        }

        /// <summary>Registers the swatch for a colour (tint/shade already applied) and returns its Self id.</summary>
        private string UseColor(PubColor c)
        {
            if (c == null) return "Swatch/None";
            var sw = new Swatch();
            if (c.Kind == PubColorKind.Cmyk && Math.Abs(c.Tint) < 1e-6)
            {
                sw.Space = "CMYK";
                sw.Value = N(c.C) + " " + N(c.M) + " " + N(c.Y) + " " + N(c.K);
                sw.Name = "C=" + N(c.C) + " M=" + N(c.M) + " Y=" + N(c.Y) + " K=" + N(c.K);
            }
            else
            {
                int r = c.R, g = c.G, b = c.B;
                if (c.Tint > 1e-6) { r = Lerp(r, 255, c.Tint); g = Lerp(g, 255, c.Tint); b = Lerp(b, 255, c.Tint); }
                else if (c.Tint < -1e-6) { r = Lerp(r, 0, -c.Tint); g = Lerp(g, 0, -c.Tint); b = Lerp(b, 0, -c.Tint); }
                sw.Space = "RGB";
                sw.Value = r + " " + g + " " + b;
                if (c.Kind == PubColorKind.Scheme && Math.Abs(c.Tint) < 1e-6 && c.SchemeIndex >= 1 && c.SchemeIndex <= 8)
                    sw.Name = SchemeColorName(c.SchemeIndex);
                else
                    sw.Name = "R=" + r + " G=" + g + " B=" + b;
            }
            sw.Self = "Color/" + Esc(sw.Name);
            Swatch existing;
            if (_swatches.TryGetValue(sw.Self, out existing)) return existing.Self;
            _swatches[sw.Self] = sw;
            _swatchOrder.Add(sw);
            return sw.Self;
        }

        private string GradientSwatch(PubFill f)
        {
            string a = UseColor(f.Fore), b = UseColor(f.Back ?? f.Fore);
            bool reverse = f.GradientVariant == 2 || f.GradientVariant == 4;
            var sw = new Swatch { IsGradient = true, Radial = f.GradientStyle == 7 || f.GradientStyle == 5, Stop0 = reverse ? b : a, Stop1 = reverse ? a : b };
            sw.Name = "Gradient " + sw.Stop0.Substring(6) + " to " + sw.Stop1.Substring(6) + (sw.Radial ? " radial" : "");
            sw.Self = "Gradient/" + Esc(sw.Name);
            if (!_swatches.ContainsKey(sw.Self)) { _swatches[sw.Self] = sw; _swatchOrder.Add(sw); }
            return sw.Self;
        }

        private string SchemeColorName(int idx)
        {
            string[] names = { "", "Main", "Accent 1", "Accent 2", "Accent 3", "Accent 4", "Hyperlink", "Followed Hyperlink", "Accent 5" };
            string scheme = string.IsNullOrEmpty(_doc.SchemeName) ? "Scheme" : _doc.SchemeName;
            return scheme + " " + names[idx];
        }

        // ================================================================ parts

        private delegate void PartBody(XmlWriter w);

        private static void AddPart(ZipArchive zip, string path, PartBody body)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false };
            using (var s = entry.Open())
            using (var w = XmlWriter.Create(s, settings))
            {
                body(w);
            }
        }

        private static void Part(XmlWriter w, string kind, PartBody body)
        {
            w.WriteStartDocument(true);
            w.WriteStartElement("idPkg", kind, NsPkg);
            w.WriteAttributeString("DOMVersion", "16.0");
            body(w);
            w.WriteEndElement();
            w.WriteEndDocument();
        }

        private static void WriteContainer(XmlWriter w)
        {
            w.WriteStartDocument(true);
            w.WriteStartElement("container", "urn:oasis:names:tc:opendocument:xmlns:container");
            w.WriteAttributeString("version", "1.0");
            w.WriteStartElement("rootfiles");
            w.WriteStartElement("rootfile");
            w.WriteAttributeString("full-path", "designmap.xml");
            w.WriteAttributeString("media-type", "text/xml");
            w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndDocument();
        }

        private void WriteDesignMap(XmlWriter w)
        {
            w.WriteStartDocument(true);
            w.WriteProcessingInstruction("aid", "style=\"50\" type=\"document\" readerVersion=\"6.0\" featureSet=\"257\" product=\"16.0(1)\" ");
            w.WriteStartElement("Document");
            w.WriteAttributeString("xmlns", "idPkg", null, NsPkg);
            w.WriteAttributeString("DOMVersion", "16.0");
            w.WriteAttributeString("Self", "d");
            var storyList = new StringBuilder();
            foreach (var st in _stories) { if (storyList.Length > 0) storyList.Append(' '); storyList.Append(st.Self); }
            w.WriteAttributeString("StoryList", storyList.ToString());
            w.WriteAttributeString("Name", Path.GetFileNameWithoutExtension(_doc.SourcePath ?? "publication") + ".indd");
            w.WriteAttributeString("ZeroPoint", "0 0");
            w.WriteAttributeString("ActiveLayer", LayerId);

            Include(w, "Graphic", "Resources/Graphic.xml");
            Include(w, "Fonts", "Resources/Fonts.xml");
            Include(w, "Styles", "Resources/Styles.xml");
            w.WriteStartElement("NumberingList");
            w.WriteAttributeString("Self", "NumberingList/$ID/[Default]");
            w.WriteAttributeString("Name", "$ID/[Default]");
            w.WriteAttributeString("ContinueNumbersAcrossStories", "false");
            w.WriteAttributeString("ContinueNumbersAcrossDocuments", "false");
            w.WriteEndElement();
            Include(w, "Preferences", "Resources/Preferences.xml");

            w.WriteStartElement("Layer");
            w.WriteAttributeString("Self", LayerId);
            w.WriteAttributeString("Name", "Layer 1");
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("Locked", "false");
            w.WriteAttributeString("IgnoreWrap", "false");
            w.WriteAttributeString("ShowGuides", "true");
            w.WriteAttributeString("LockGuides", "false");
            w.WriteAttributeString("UI", "true");
            w.WriteAttributeString("Expendable", "true");
            w.WriteAttributeString("Printable", "true");
            w.WriteStartElement("Properties");
            w.WriteStartElement("LayerColor"); w.WriteAttributeString("type", "enumeration"); w.WriteString("LightBlue"); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();

            foreach (var sp in _spreads) if (sp.IsMaster) Include(w, "MasterSpread", "MasterSpreads/MasterSpread_" + sp.Self + ".xml");
            foreach (var sp in _spreads) if (!sp.IsMaster) Include(w, "Spread", "Spreads/Spread_" + sp.Self + ".xml");

            WriteSections(w);

            foreach (var st in _stories) Include(w, "Story", "Stories/Story_" + st.Self + ".xml");

            foreach (var h in _hyperlinks)
            {
                w.WriteStartElement("HyperlinkURLDestination");
                w.WriteAttributeString("Self", h.DestSelf);
                w.WriteAttributeString("Name", h.Url);
                w.WriteAttributeString("DestinationURL", h.Url);
                w.WriteAttributeString("DestinationUniqueKey", N(h.Key));
                w.WriteAttributeString("Hidden", "false");
                w.WriteEndElement();
            }
            foreach (var h in _hyperlinks)
            {
                w.WriteStartElement("Hyperlink");
                w.WriteAttributeString("Self", "uh" + h.Key);
                w.WriteAttributeString("Name", h.Name);
                w.WriteAttributeString("Source", h.SourceSelf);
                w.WriteAttributeString("Visible", "false");
                w.WriteAttributeString("Highlight", "None");
                w.WriteAttributeString("Width", "Thin");
                w.WriteAttributeString("BorderStyle", "Solid");
                w.WriteAttributeString("Hidden", "false");
                w.WriteAttributeString("DestinationUniqueKey", N(h.Key));
                w.WriteStartElement("Properties");
                w.WriteStartElement("BorderColor"); w.WriteAttributeString("type", "enumeration"); w.WriteString("Black"); w.WriteEndElement();
                w.WriteStartElement("Destination"); w.WriteAttributeString("type", "object"); w.WriteString(h.DestSelf); w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        }

        private static void Include(XmlWriter w, string kind, string src)
        {
            w.WriteStartElement("idPkg", kind, NsPkg);
            w.WriteAttributeString("src", src);
            w.WriteEndElement();
        }

        private void WriteSections(XmlWriter w)
        {
            var sections = new List<PubSection>(_doc.Sections);
            if (sections.Count == 0) sections.Add(new PubSection { StartPageIndex = 1, PageNumberStart = 1 });
            int n = 0;
            foreach (var sec in sections)
            {
                PubPage page = null;
                foreach (var p in _doc.Pages) if (p.Index == sec.StartPageIndex) { page = p; break; }
                if (page == null) continue;
                n++;
                w.WriteStartElement("Section");
                w.WriteAttributeString("Self", "usec" + n);
                w.WriteAttributeString("Length", N(SectionLength(sections, sec)));
                w.WriteAttributeString("Name", "");
                w.WriteAttributeString("ContinueNumbering", sec.ContinueFromPrevious && n > 1 ? "true" : "false");
                w.WriteAttributeString("IncludeSectionPrefix", "false");
                w.WriteAttributeString("PageNumberStart", N(Math.Max(1, sec.PageNumberStart)));
                w.WriteAttributeString("Marker", "");
                w.WriteAttributeString("PageStart", "up" + page.Id);
                w.WriteAttributeString("SectionPrefix", "");
                w.WriteStartElement("Properties");
                w.WriteStartElement("PageNumberStyle"); w.WriteAttributeString("type", "enumeration"); w.WriteString(PageNumberStyle(sec.PageNumberFormat)); w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();
            }
        }

        private int SectionLength(List<PubSection> all, PubSection sec)
        {
            int end = _doc.Pages.Count + 1;
            foreach (var o in all) if (o.StartPageIndex > sec.StartPageIndex && o.StartPageIndex < end) end = o.StartPageIndex;
            return Math.Max(1, end - sec.StartPageIndex);
        }

        private static string PageNumberStyle(int fmt)
        {
            switch (fmt) { case 1: return "UpperRoman"; case 2: return "LowerRoman"; case 3: return "UpperLetters"; case 4: return "LowerLetters"; default: return "Arabic"; }
        }

        // --------------------------------------------------------------- Graphic

        private void WriteGraphic(XmlWriter w)
        {
            Color(w, "Color/Black", "Black", "CMYK", "0 0 0 100", "Specialblack");
            Color(w, "Color/Paper", "Paper", "CMYK", "0 0 0 0", "Specialpaper");
            Color(w, "Color/Registration", "Registration", "CMYK", "100 100 100 100", "Specialregistration");
            foreach (var sw in _swatchOrder) if (!sw.IsGradient) Color(w, sw.Self, sw.Name, sw.Space, sw.Value, "Normal");
            foreach (var sw in _swatchOrder)
            {
                if (!sw.IsGradient) continue;
                w.WriteStartElement("Gradient");
                w.WriteAttributeString("Self", sw.Self);
                w.WriteAttributeString("Type", sw.Radial ? "Radial" : "Linear");
                w.WriteAttributeString("Name", sw.Name);
                w.WriteAttributeString("ColorEditable", "true");
                w.WriteAttributeString("ColorRemovable", "true");
                w.WriteAttributeString("Visible", "true");
                w.WriteAttributeString("SwatchCreatorID", "7937");
                GradientStop(w, sw.Self + "/0", sw.Stop0, 0);
                GradientStop(w, sw.Self + "/1", sw.Stop1, 100);
                w.WriteEndElement();
            }
            w.WriteStartElement("Swatch");
            w.WriteAttributeString("Self", "Swatch/None");
            w.WriteAttributeString("Name", "None");
            w.WriteAttributeString("ColorEditable", "false");
            w.WriteAttributeString("ColorRemovable", "false");
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("SwatchCreatorID", "7937");
            w.WriteEndElement();
            StrokeStyle(w, "StrokeStyle/$ID/Solid", "$ID/Solid");
            StrokeStyle(w, "StrokeStyle/$ID/Dashed", "$ID/Dashed");
            StrokeStyle(w, "StrokeStyle/$ID/Canned Dotted", "$ID/Canned Dotted");
            StrokeStyle(w, "StrokeStyle/$ID/ThinThin", "$ID/Thin - Thin");
            StrokeStyle(w, "StrokeStyle/$ID/ThickThin", "$ID/Thick - Thin");
            StrokeStyle(w, "StrokeStyle/$ID/ThinThick", "$ID/Thin - Thick");
            StrokeStyle(w, "StrokeStyle/$ID/ThickThinThick", "$ID/Thick - Thin - Thick");
            StrokeStyle(w, "StrokeStyle/$ID/ThinThickThin", "$ID/Thin - Thick - Thin");
        }

        private static void Color(XmlWriter w, string self, string name, string space, string value, string overrideKind)
        {
            w.WriteStartElement("Color");
            w.WriteAttributeString("Self", self);
            w.WriteAttributeString("Model", "Process");
            w.WriteAttributeString("Space", space);
            w.WriteAttributeString("ColorValue", value);
            w.WriteAttributeString("ColorOverride", overrideKind);
            w.WriteAttributeString("AlternateSpace", "NoAlternateColor");
            w.WriteAttributeString("AlternateColorValue", "");
            w.WriteAttributeString("Name", name);
            w.WriteAttributeString("ColorEditable", "true");
            w.WriteAttributeString("ColorRemovable", overrideKind == "Normal" ? "true" : "false");
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("SwatchCreatorID", "7937");
            w.WriteEndElement();
        }

        private static void GradientStop(XmlWriter w, string self, string color, double location)
        {
            w.WriteStartElement("GradientStop");
            w.WriteAttributeString("Self", self);
            w.WriteAttributeString("StopColor", color);
            w.WriteAttributeString("Location", N(location));
            w.WriteAttributeString("Midpoint", "50");
            w.WriteEndElement();
        }

        private static void StrokeStyle(XmlWriter w, string self, string name)
        {
            w.WriteStartElement("StrokeStyle");
            w.WriteAttributeString("Self", self);
            w.WriteAttributeString("Name", name);
            w.WriteEndElement();
        }

        // ----------------------------------------------------------------- Fonts

        private void WriteFonts(XmlWriter w)
        {
            var names = new List<string>(_fonts);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                w.WriteStartElement("FontFamily");
                w.WriteAttributeString("Self", "di" + (_nextId++));
                w.WriteAttributeString("Name", name);
                foreach (string style in new[] { "Regular", "Bold", "Italic", "Bold Italic" })
                {
                    w.WriteStartElement("Font");
                    w.WriteAttributeString("Self", "di" + (_nextId++));
                    w.WriteAttributeString("FontFamily", name);
                    w.WriteAttributeString("Name", name + (style == "Regular" ? "" : " " + style));
                    w.WriteAttributeString("PostScriptName", "$ID/" + name.Replace(" ", "") + (style == "Regular" ? "" : "-" + style.Replace(" ", "")));
                    w.WriteAttributeString("Status", "NotAvailable");
                    w.WriteAttributeString("FontStyleName", "$ID/" + style);
                    w.WriteAttributeString("FontType", "OpenTypeTT");
                    w.WriteAttributeString("WritingScript", "0");
                    w.WriteAttributeString("FullName", "$ID/" + name + " " + style);
                    w.WriteAttributeString("FontStyleNameNative", "$ID/" + style);
                    w.WriteAttributeString("FontFamilyName", "$ID/" + name);
                    w.WriteAttributeString("FullNameNative", "$ID/" + name + " " + style);
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
        }

        // ---------------------------------------------------------------- Styles

        private void WriteStyles(XmlWriter w)
        {
            w.WriteStartElement("RootCharacterStyleGroup");
            w.WriteAttributeString("Self", "ucsg");
            w.WriteStartElement("CharacterStyle");
            w.WriteAttributeString("Self", NoCharStyle);
            w.WriteAttributeString("Name", "$ID/[No character style]");
            w.WriteAttributeString("Imported", "false");
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("RootParagraphStyleGroup");
            w.WriteAttributeString("Self", "upsg");
            w.WriteStartElement("ParagraphStyle");
            w.WriteAttributeString("Self", NoParaStyle);
            w.WriteAttributeString("Name", "$ID/[No paragraph style]");
            w.WriteAttributeString("Imported", "false");
            w.WriteAttributeString("NextStyle", NoParaStyle);
            w.WriteAttributeString("PointSize", "12");
            w.WriteAttributeString("FillColor", "Color/Black");
            w.WriteAttributeString("FontStyle", "Regular");
            w.WriteAttributeString("Justification", "LeftAlign");
            w.WriteStartElement("Properties");
            PropString(w, "BasedOn", "$ID/[No paragraph style]");
            PropEnum(w, "Leading", "Auto");
            PropString(w, "AppliedFont", "Minion Pro");
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("ParagraphStyle");
            w.WriteAttributeString("Self", "ParagraphStyle/$ID/NormalParagraphStyle");
            w.WriteAttributeString("Name", "$ID/NormalParagraphStyle");
            w.WriteAttributeString("Imported", "false");
            w.WriteAttributeString("NextStyle", "ParagraphStyle/$ID/NormalParagraphStyle");
            w.WriteStartElement("Properties");
            PropString(w, "BasedOn", "$ID/[No paragraph style]");
            w.WriteEndElement();
            w.WriteEndElement();

            foreach (var st in _doc.Styles)
            {
                w.WriteStartElement("ParagraphStyle");
                w.WriteAttributeString("Self", StyleSelf(st.Name));
                w.WriteAttributeString("Name", st.Name);
                w.WriteAttributeString("Imported", "false");
                string next = !string.IsNullOrEmpty(st.NextStyle) && _styleByName.ContainsKey(st.NextStyle) ? StyleSelf(st.NextStyle) : StyleSelf(st.Name);
                w.WriteAttributeString("NextStyle", next);
                double size = st.Font != null ? st.Font.Size : 10;
                WriteCharAttributes(w, st.Font, null);
                WriteParaAttributes(w, st.Paragraph, null, size);
                w.WriteStartElement("Properties");
                string based = !string.IsNullOrEmpty(st.BaseStyle) && _styleByName.ContainsKey(st.BaseStyle) ? StyleSelf(st.BaseStyle) : "$ID/[No paragraph style]";
                PropString(w, "BasedOn", based);
                if (st.Font != null) PropString(w, "AppliedFont", st.Font.Name);
                WriteLeadingProp(w, st.Paragraph, size);
                WriteTabList(w, st.Paragraph);
                w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();

            w.WriteStartElement("RootCellStyleGroup"); w.WriteAttributeString("Self", "ucellsg");
            w.WriteStartElement("CellStyle"); w.WriteAttributeString("Self", "CellStyle/$ID/[None]"); w.WriteAttributeString("Name", "$ID/[None]"); w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("RootTableStyleGroup"); w.WriteAttributeString("Self", "utablesg");
            w.WriteStartElement("TableStyle"); w.WriteAttributeString("Self", "TableStyle/$ID/[No table style]"); w.WriteAttributeString("Name", "$ID/[No table style]"); w.WriteEndElement();
            w.WriteStartElement("TableStyle"); w.WriteAttributeString("Self", "TableStyle/$ID/[Basic Table]"); w.WriteAttributeString("Name", "$ID/[Basic Table]"); w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("RootObjectStyleGroup"); w.WriteAttributeString("Self", "uosg");
            foreach (string n in new[] { "$ID/[None]", "$ID/[Normal Graphics Frame]", "$ID/[Normal Text Frame]" })
            {
                w.WriteStartElement("ObjectStyle");
                w.WriteAttributeString("Self", "ObjectStyle/" + n);
                w.WriteAttributeString("Name", n);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        private static string StyleSelf(string name) { return "ParagraphStyle/" + Esc(name); }

        /// <summary>Character attributes; when <paramref name="baseline"/> is given only differences are written.</summary>
        private void WriteCharAttributes(XmlWriter w, PubFont f, PubFont baseline)
        {
            if (f == null) return;
            bool all = baseline == null;
            if (all || Math.Abs(f.Size - baseline.Size) > 1e-6) w.WriteAttributeString("PointSize", N(f.Size));
            if (all || f.Bold != baseline.Bold || f.Italic != baseline.Italic) w.WriteAttributeString("FontStyle", FontStyle(f));
            string color = UseColor(f.Color);
            if (all || color != UseColor(baseline.Color)) w.WriteAttributeString("FillColor", f.Color == null ? "Color/Black" : color);
            if (all || (f.Underline != 0) != (baseline.Underline != 0)) w.WriteAttributeString("Underline", f.Underline != 0 ? "true" : "false");
            if (all || f.StrikeThrough != baseline.StrikeThrough) w.WriteAttributeString("StrikeThru", f.StrikeThrough ? "true" : "false");
            string caps = f.AllCaps ? "AllCaps" : f.SmallCaps ? "SmallCaps" : "Normal";
            string bcaps = baseline == null ? "" : baseline.AllCaps ? "AllCaps" : baseline.SmallCaps ? "SmallCaps" : "Normal";
            if (all || caps != bcaps) w.WriteAttributeString("Capitalization", caps);
            string pos = f.Superscript ? "Superscript" : f.Subscript ? "Subscript" : "Normal";
            string bpos = baseline == null ? "" : baseline.Superscript ? "Superscript" : baseline.Subscript ? "Subscript" : "Normal";
            if (all || pos != bpos) w.WriteAttributeString("Position", pos);
            if (all || Math.Abs(f.Tracking - baseline.Tracking) > 1e-6) w.WriteAttributeString("Tracking", N((f.Tracking - 100) * 10));
            if (all || Math.Abs(f.Scaling - baseline.Scaling) > 1e-6) w.WriteAttributeString("HorizontalScale", N(f.Scaling <= 0 ? 100 : f.Scaling));
            if (all || Math.Abs(f.Position - baseline.Position) > 1e-6) w.WriteAttributeString("BaselineShift", N(f.Position));
        }

        private static string FontStyle(PubFont f)
        {
            return f.Bold && f.Italic ? "Bold Italic" : f.Bold ? "Bold" : f.Italic ? "Italic" : "Regular";
        }

        private void WriteParaAttributes(XmlWriter w, PubParagraphFormat p, PubParagraphFormat baseline, double size)
        {
            if (p == null) return;
            bool all = baseline == null;
            if (all || p.Alignment != baseline.Alignment) w.WriteAttributeString("Justification", Justification(p.Alignment));
            if (all || Math.Abs(p.LeftIndent - baseline.LeftIndent) > 1e-6) w.WriteAttributeString("LeftIndent", N(p.LeftIndent));
            if (all || Math.Abs(p.RightIndent - baseline.RightIndent) > 1e-6) w.WriteAttributeString("RightIndent", N(p.RightIndent));
            if (all || Math.Abs(p.FirstLineIndent - baseline.FirstLineIndent) > 1e-6) w.WriteAttributeString("FirstLineIndent", N(p.FirstLineIndent));
            if (all || Math.Abs(p.SpaceBefore - baseline.SpaceBefore) > 1e-6) w.WriteAttributeString("SpaceBefore", N(p.SpaceBefore));
            if (all || Math.Abs(p.SpaceAfter - baseline.SpaceAfter) > 1e-6) w.WriteAttributeString("SpaceAfter", N(p.SpaceAfter));
            if (all || p.KeepWithNext != baseline.KeepWithNext) w.WriteAttributeString("KeepWithNext", p.KeepWithNext ? "1" : "0");
            if (all || p.KeepLinesTogether != baseline.KeepLinesTogether) w.WriteAttributeString("KeepLinesTogether", p.KeepLinesTogether ? "true" : "false");
            if (p.DropCapLines > 0 && (all || p.DropCapLines != baseline.DropCapLines))
            {
                w.WriteAttributeString("DropCapLines", N(p.DropCapLines));
                w.WriteAttributeString("DropCapCharacters", N(Math.Max(1, p.DropCapSpan)));
            }
            string list = ListType(p.ListType), blist = baseline == null ? "" : ListType(baseline.ListType);
            if (all || list != blist) w.WriteAttributeString("BulletsAndNumberingListType", list);
            if (list == "NumberedList" && (all || p.ListNumberStart != baseline.ListNumberStart))
                w.WriteAttributeString("NumberingStartAt", N(Math.Max(1, p.ListNumberStart)));
        }

        private static string ListType(int t)
        {
            if (t == 23) return "BulletList";
            if (t == 255 || t < 0) return "NoList";
            return "NumberedList";
        }

        private static string Justification(int a)
        {
            switch (a)
            {
                case 1: return "CenterAlign";
                case 2: return "RightAlign";
                case 3: case 6: case 7: case 8: case 11: return "LeftJustified";
                case 4: case 5: case 9: case 10: return "FullyJustified";
                default: return "LeftAlign";
            }
        }

        /// <summary>Leading in points: Publisher's measured pitch when known, else an estimate from the rule.</summary>
        private static double LeadingOf(PubParagraphFormat p, double size)
        {
            if (p == null) return 1.2 * size;
            if (p.MeasuredLeading > 0) return p.MeasuredLeading;
            switch (p.LineSpacingRule)
            {
                case 4: return p.LineSpacing;
                case 1: return 1.5 * 1.2 * size;
                case 2: return 2.0 * 1.2 * size;
                case 5: return p.LineSpacing * 1.2 * size;
                default: return 1.2 * size;
            }
        }

        private static void WriteLeadingProp(XmlWriter w, PubParagraphFormat p, double size)
        {
            if (p == null) return;
            double lead = LeadingOf(p, size);
            if (lead <= 0 || size <= 0) { PropEnum(w, "Leading", "Auto"); return; }
            w.WriteStartElement("Leading"); w.WriteAttributeString("type", "unit"); w.WriteString(N(lead)); w.WriteEndElement();
        }

        private static void WriteTabList(XmlWriter w, PubParagraphFormat p)
        {
            if (p == null || p.Tabs.Count == 0) return;
            w.WriteStartElement("TabList"); w.WriteAttributeString("type", "list");
            foreach (var t in p.Tabs)
            {
                w.WriteStartElement("ListItem"); w.WriteAttributeString("type", "record");
                string align = t.Alignment == 1 ? "CenterAlign" : t.Alignment == 2 ? "RightAlign" : t.Alignment == 3 ? "CharacterAlign" : "LeftAlign";
                w.WriteStartElement("Alignment"); w.WriteAttributeString("type", "enumeration"); w.WriteString(align); w.WriteEndElement();
                w.WriteStartElement("AlignmentCharacter"); w.WriteAttributeString("type", "string"); w.WriteString("."); w.WriteEndElement();
                string leader = t.Leader == 1 ? "." : t.Leader == 2 ? "-" : t.Leader == 3 ? "_" : t.Leader == 5 ? "•" : "";
                w.WriteStartElement("Leader"); w.WriteAttributeString("type", "string"); w.WriteString(leader); w.WriteEndElement();
                w.WriteStartElement("Position"); w.WriteAttributeString("type", "unit"); w.WriteString(N(t.Position)); w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        private static void PropString(XmlWriter w, string name, string value)
        {
            w.WriteStartElement(name); w.WriteAttributeString("type", "string"); w.WriteString(value); w.WriteEndElement();
        }
        private static void PropEnum(XmlWriter w, string name, string value)
        {
            w.WriteStartElement(name); w.WriteAttributeString("type", "enumeration"); w.WriteString(value); w.WriteEndElement();
        }

        // ----------------------------------------------------------- Preferences

        private void WritePreferences(XmlWriter w)
        {
            w.WriteStartElement("DocumentPreference");
            w.WriteAttributeString("PageHeight", N(_doc.PageHeight));
            w.WriteAttributeString("PageWidth", N(_doc.PageWidth));
            w.WriteAttributeString("PagesPerDocument", N(Math.Max(1, _doc.Pages.Count)));
            w.WriteAttributeString("FacingPages", _doc.FacingPages ? "true" : "false");
            w.WriteAttributeString("DocumentBleedTopOffset", "0");
            w.WriteAttributeString("DocumentBleedBottomOffset", "0");
            w.WriteAttributeString("DocumentBleedInsideOrLeftOffset", "0");
            w.WriteAttributeString("DocumentBleedOutsideOrRightOffset", "0");
            w.WriteAttributeString("DocumentBleedUniformSize", "true");
            w.WriteAttributeString("SlugTopOffset", "0");
            w.WriteAttributeString("SlugBottomOffset", "0");
            w.WriteAttributeString("SlugInsideOrLeftOffset", "0");
            w.WriteAttributeString("SlugRightOrOutsideOffset", "0");
            w.WriteAttributeString("PageBinding", "LeftToRight");
            w.WriteAttributeString("Intent", "PrintIntent");
            w.WriteAttributeString("OverprintBlack", "true");
            w.WriteAttributeString("CreatePrimaryTextFrame", "false");
            w.WriteEndElement();

            MarginPreference(w, _doc.MarginTop, _doc.MarginBottom, _doc.MarginLeft, _doc.MarginRight, _doc.Columns, _doc.ColumnGutter);

            w.WriteStartElement("ViewPreference");
            w.WriteAttributeString("HorizontalMeasurementUnits", "Points");
            w.WriteAttributeString("VerticalMeasurementUnits", "Points");
            w.WriteAttributeString("RulerOrigin", "SpreadOrigin");
            w.WriteAttributeString("PointsPerInch", "72");
            w.WriteAttributeString("CursorKeyIncrement", "1");
            w.WriteEndElement();

            w.WriteStartElement("GridPreference");
            w.WriteAttributeString("BaselineStart", N(_doc.BaselineOffset));
            w.WriteAttributeString("BaselineDivision", N(_doc.BaselineSpacing > 0 ? _doc.BaselineSpacing : 12));
            w.WriteAttributeString("BaselineViewThreshold", "75");
            w.WriteAttributeString("BaselineGridRelativeOption", "TopOfPage");
            w.WriteAttributeString("BaselineShown", "false");
            w.WriteAttributeString("DocumentGridShown", "false");
            w.WriteAttributeString("GridsInBack", "true");
            w.WriteEndElement();
        }

        private static void MarginPreference(XmlWriter w, double top, double bottom, double left, double right, int cols, double gutter)
        {
            w.WriteStartElement("MarginPreference");
            w.WriteAttributeString("ColumnCount", N(Math.Max(1, cols)));
            w.WriteAttributeString("ColumnGutter", N(gutter));
            w.WriteAttributeString("Top", N(top));
            w.WriteAttributeString("Bottom", N(bottom));
            w.WriteAttributeString("Left", N(left));
            w.WriteAttributeString("Right", N(right));
            w.WriteAttributeString("ColumnDirection", "Horizontal");
            w.WriteAttributeString("ColumnsPositions", "0 " + N(Math.Max(0, 0)));
            w.WriteEndElement();
        }

        // --------------------------------------------------------------- Spreads

        private void WriteSpread(XmlWriter w, SpreadOut sp)
        {
            w.WriteStartElement(sp.IsMaster ? "MasterSpread" : "Spread");
            w.WriteAttributeString("Self", sp.Self);
            if (sp.IsMaster)
            {
                string prefix = Letter(_doc.Masters.IndexOf(sp.Master));
                string baseName = string.IsNullOrEmpty(sp.Master.Name) ? "Master" : sp.Master.Name;
                w.WriteAttributeString("Name", prefix + "-" + baseName);
                w.WriteAttributeString("NamePrefix", prefix);
                w.WriteAttributeString("BaseName", baseName);
                w.WriteAttributeString("ShowMasterItems", "true");
            }
            else
            {
                w.WriteAttributeString("FlattenerOverride", "Default");
                w.WriteAttributeString("AllowPageShuffle", "true");
                w.WriteAttributeString("ShowMasterItems", "true");
                int leftCount = 0; foreach (var pg in sp.Pages) if (pg.Left) leftCount++;
                w.WriteAttributeString("BindingLocation", N(leftCount));
            }
            w.WriteAttributeString("PageCount", N(sp.Pages.Count));
            w.WriteAttributeString("ItemTransform", "1 0 0 1 0 0");

            // IDML lists pages first, then items in z-order.
            int pageIndex = 0;
            foreach (var pg in sp.Pages)
            {
                pageIndex++;
                var page = pg.Page;
                w.WriteStartElement("Page");
                w.WriteAttributeString("Self", sp.IsMaster ? "ump" + page.Id + "_" + pageIndex : "up" + page.Id);
                w.WriteAttributeString("Name", sp.IsMaster ? N(pageIndex) : (string.IsNullOrEmpty(page.PageNumber) ? N(page.Index) : page.PageNumber));
                w.WriteAttributeString("AppliedTrapPreset", "TrapPreset/$ID/kDefaultTrapStyleName");
                w.WriteAttributeString("OverrideList", "");
                w.WriteAttributeString("AppliedMaster", !sp.IsMaster && page.MasterId != 0 && !page.IgnoreMaster ? "um" + page.MasterId : "n");
                w.WriteAttributeString("GeometricBounds", "0 0 " + N(page.Height) + " " + N(page.Width));
                w.WriteAttributeString("ItemTransform", "1 0 0 1 " + N(pg.X - sp.Ox) + " " + N(-sp.Oy));
                MarginPreference(w, page.MarginTop, page.MarginBottom, page.MarginLeft, page.MarginRight, page.Columns, page.ColumnGutter);
                w.WriteEndElement();
            }

            foreach (var pg in sp.Pages)
            {
                double dx = pg.X - sp.Ox, dy = -sp.Oy;
                if (pg.Page.Background != null && pg.Page.Background.Visible)
                {
                    var bg = new PubShape { Id = unchecked(pg.Page.Id * 7 + 1), Name = "Page background", Kind = PubShapeKind.Rectangle, Left = 0, Top = 0, Width = pg.Page.Width, Height = pg.Page.Height, Fill = pg.Page.Background };
                    WriteItem(w, bg, dx, dy);
                }
                var shapes = new List<PubShape>(pg.Page.Shapes);
                shapes.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
                _wrapBoxes = new List<WrapBox>();
                CollectWrapBoxes(shapes, null);
                foreach (var sh in shapes) { _currentTopZ = sh.ZOrder; WriteItem(w, sh, dx, dy); }
                // A two-page master repeats its single shape list; only emit items on the first page.
                if (sp.IsMaster) break;
            }
            if (!sp.IsMaster && _spreads.IndexOf(sp) == FirstDocSpreadIndex())
            {
                foreach (var sh in _doc.ScratchShapes) WriteItem(w, sh, sp.Pages[0].X - sp.Ox, -sp.Oy);
            }
            w.WriteEndElement();
        }

        private void CollectWrapBoxes(List<PubShape> shapes, int? topZ)
        {
            foreach (var sh in shapes)
            {
                int z = topZ ?? sh.ZOrder;
                if (sh.Wrap != null && (sh.Wrap.Type == 1 || sh.Wrap.Type == 2 || sh.Wrap.Type == 4) && sh.TextFrame == null && sh.Children == null)
                {
                    double[] b = Bounds(sh);
                    _wrapBoxes.Add(new WrapBox { X0 = b[0] - sh.Wrap.Left, Y0 = b[1] - sh.Wrap.Top, X1 = b[2] + sh.Wrap.Right, Y1 = b[3] + sh.Wrap.Bottom, Z = z });
                }
                if (sh.Children != null) CollectWrapBoxes(sh.Children, z);
            }
        }

        /// <summary>Axis-aligned bounds of a shape in page coordinates, rotation included.</summary>
        private static double[] Bounds(PubShape sh)
        {
            double r = sh.Rotation * Math.PI / 180, c = Math.Abs(Math.Cos(r)), s = Math.Abs(Math.Sin(r));
            double bw = sh.Width * c + sh.Height * s, bh = sh.Width * s + sh.Height * c;
            double cx = sh.Left + sh.Width / 2, cy = sh.Top + sh.Height / 2;
            return new[] { cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2 };
        }

        private bool WrappedFromAbove(PubShape frame)
        {
            double[] b = Bounds(frame);
            foreach (var wb in _wrapBoxes)
                if (wb.Z > _currentTopZ && wb.X0 < b[2] && wb.X1 > b[0] && wb.Y0 < b[3] && wb.Y1 > b[1]) return true;
            return false;
        }

        private int FirstDocSpreadIndex()
        {
            for (int i = 0; i < _spreads.Count; i++) if (!_spreads[i].IsMaster) return i;
            return -1;
        }

        private static string Letter(int i)
        {
            if (i < 0) i = 0;
            return ((char)('A' + (i % 26))).ToString();
        }

        // ----------------------------------------------------------------- Items

        /// <summary>Writes one page item. (dx, dy) is the page-to-spread translation.</summary>
        private void WriteItem(XmlWriter w, PubShape sh, double dx, double dy)
        {
            switch (sh.Kind)
            {
                case PubShapeKind.Group: WriteGroup(w, sh, dx, dy); return;
                case PubShapeKind.Table: WriteTableItem(w, sh, dx, dy); return;
                case PubShapeKind.Picture: WritePictureItem(w, sh, dx, dy, PictureSource(sh)); return;
                case PubShapeKind.Raster: WritePictureItem(w, sh, dx, dy, sh.RasterPath); return;
            }

            bool hasText = sh.TextFrame != null && HasStoryText(sh);
            string element = hasText ? "TextFrame" : sh.Kind == PubShapeKind.Line ? "GraphicLine" : sh.Kind == PubShapeKind.Oval ? "Oval" : sh.Kind == PubShapeKind.Polygon ? "Polygon" : "Rectangle";
            // An invisible, top-aligned text frame may grow a little so lines whose descent
            // overhangs the frame in Publisher are not overset here (see OversetSlack).
            double grow = 0;
            if (hasText && sh.Nodes == null && sh.TextFrame.VerticalAlignment == 0
                && (sh.Fill == null || !sh.Fill.Visible) && (sh.Stroke == null || !sh.Stroke.Visible || sh.Stroke.Weight <= 0))
            {
                PubStory st; FrameLink fl;
                int sid = _frameLinks.TryGetValue(sh.Id, out fl) ? fl.StoryId : sh.TextFrame.StoryId;
                if (_doc.Stories.TryGetValue(sid, out st) && st.FrameIds.Count <= 1)
                    grow = Math.Max(OversetSlack(st), FrameSlack(st, sh.Height, sh.TextFrame.MarginTop + sh.TextFrame.MarginBottom));
            }
            sh.Height += grow;
            w.WriteStartElement(element);
            WriteCommonAttributes(w, sh, hasText ? "TextType" : "Unassigned", dx, dy);
            if (hasText)
            {
                FrameLink link;
                int storyId = sh.TextFrame.StoryId;
                int prev = sh.TextFrame.PreviousShapeId, next = sh.TextFrame.NextShapeId;
                if (_frameLinks.TryGetValue(sh.Id, out link)) { storyId = link.StoryId; prev = link.Prev; next = link.Next; }
                w.WriteAttributeString("ParentStory", "us" + storyId);
                w.WriteAttributeString("PreviousTextFrame", prev != 0 ? "ui" + prev : "n");
                w.WriteAttributeString("NextTextFrame", next != 0 ? "ui" + next : "n");
            }
            WriteFillStroke(w, sh);
            WriteCorners(w, sh);
            w.WriteStartElement("Properties");
            WritePath(w, sh);
            w.WriteEndElement();
            WriteTransparency(w, sh);
            if (hasText) WriteTextFramePreference(w, sh.TextFrame, !WrappedFromAbove(sh));
            WriteWrap(w, sh);
            w.WriteEndElement();
            sh.Height -= grow;
        }

        private bool HasStoryText(PubShape sh)
        {
            FrameLink link;
            int id = _frameLinks.TryGetValue(sh.Id, out link) ? link.StoryId : sh.TextFrame.StoryId;
            PubStory st;
            if (!_doc.Stories.TryGetValue(id, out st)) return false;
            return st.Text.Length > 0 || st.FrameIds.Count > 1;
        }

        private void WriteCommonAttributes(XmlWriter w, PubShape sh, string contentType, double dx, double dy)
        {
            w.WriteAttributeString("Self", "ui" + sh.Id);
            w.WriteAttributeString("ContentType", contentType);
            w.WriteAttributeString("Name", string.IsNullOrEmpty(sh.Name) ? "$ID/" : sh.Name);
            w.WriteAttributeString("ItemLayer", LayerId);
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("Locked", "false");
            w.WriteAttributeString("Nonprinting", "false");
            w.WriteAttributeString("AppliedObjectStyle", contentType == "TextType" ? "ObjectStyle/$ID/[Normal Text Frame]" : "ObjectStyle/$ID/[Normal Graphics Frame]");
            w.WriteAttributeString("ItemTransform", Transform(sh, dx, dy));
        }

        /// <summary>
        /// Inner path coordinates have their origin at the shape's unrotated top-left.
        /// Node-based shapes (lines, polygons) already carry rotation in their points.
        /// </summary>
        private static string Transform(PubShape sh, double dx, double dy)
        {
            double[] m = Identity();
            m = Mul(Translate(sh.Left + dx, sh.Top + dy), m);
            // A flipped text box still shows its text the right way round in Publisher, so flips
            // only matter for graphics.
            bool flipH = sh.FlipHorizontal && sh.TextFrame == null, flipV = sh.FlipVertical && sh.TextFrame == null;
            if (sh.Nodes == null && (Math.Abs(sh.Rotation) > 1e-6 || flipH || flipV))
            {
                double cx = sh.Width / 2, cy = sh.Height / 2;
                double[] about = Translate(cx, cy);
                if (Math.Abs(sh.Rotation) > 1e-6) about = Mul(about, Rotate(sh.Rotation));
                if (flipH || flipV) about = Mul(about, Scale(flipH ? -1 : 1, flipV ? -1 : 1));
                about = Mul(about, Translate(-cx, -cy));
                m = Mul(m, about);
            }
            return Matrix(m);
        }

        private void WriteFillStroke(XmlWriter w, PubShape sh)
        {
            string fill = "Swatch/None";
            double fillTint = -1;
            if (sh.Fill != null && sh.Fill.Visible && sh.Fill.Type != 2 && sh.Fill.Type != 4 && sh.Fill.Type != 6)
            {
                if (sh.Fill.Type == 3)
                {
                    fill = GradientSwatch(sh.Fill);
                    w.WriteAttributeString("GradientFillAngle", N(GradientAngle(sh.Fill)));
                }
                else if (sh.Fill.Fore != null) fill = UseColor(sh.Fill.Fore);
            }
            w.WriteAttributeString("FillColor", fill);
            w.WriteAttributeString("FillTint", N(fillTint));
            w.WriteAttributeString("OverprintFill", "false");

            if (sh.Stroke != null && sh.Stroke.Visible && sh.Stroke.Weight > 0 && sh.Stroke.Color != null)
            {
                w.WriteAttributeString("StrokeColor", UseColor(sh.Stroke.Color));
                w.WriteAttributeString("StrokeWeight", N(sh.Stroke.Weight));
                w.WriteAttributeString("StrokeTint", "-1");
                w.WriteAttributeString("StrokeType", StrokeType(sh.Stroke));
                w.WriteAttributeString("StrokeAlignment", "CenterAlignment");
                w.WriteAttributeString("EndCap", sh.Stroke.CapStyle == 1 ? "RoundEndCap" : sh.Stroke.CapStyle == 2 ? "ProjectingEndCap" : "ButtEndCap");
                w.WriteAttributeString("EndJoin", sh.Stroke.JoinStyle == 1 ? "BevelEndJoin" : sh.Stroke.JoinStyle == 2 ? "MiterEndJoin" : "RoundEndJoin");
                w.WriteAttributeString("MiterLimit", "4");
                if (sh.Kind == PubShapeKind.Line || sh.Kind == PubShapeKind.Polygon)
                {
                    w.WriteAttributeString("LeftLineEnd", ArrowHead(sh.Stroke.BeginArrow));
                    w.WriteAttributeString("RightLineEnd", ArrowHead(sh.Stroke.EndArrow));
                }
            }
            else
            {
                w.WriteAttributeString("StrokeColor", "Swatch/None");
                w.WriteAttributeString("StrokeWeight", "0");
            }
            w.WriteAttributeString("OverprintStroke", "false");
            w.WriteAttributeString("GapColor", "Swatch/None");
        }

        private static double GradientAngle(PubFill f)
        {
            // Office names gradients by their bands: "horizontal" = horizontal bands, i.e. the
            // colour runs top to bottom. IDML angles are counter-clockwise from left-to-right.
            switch (f.GradientStyle)
            {
                case 2: return 0;        // vertical bands: left to right
                case 3: return 45;       // diagonal up
                case 4: return -45;      // diagonal down
                default: return -90;     // horizontal bands: top to bottom
            }
        }

        private static string StrokeType(PubStroke s)
        {
            switch (s.LineStyle)
            {
                case 2: return "StrokeStyle/$ID/ThinThin";
                case 3: return "StrokeStyle/$ID/ThinThick";
                case 4: return "StrokeStyle/$ID/ThickThin";
                case 5: return "StrokeStyle/$ID/ThinThickThin";
            }
            switch (s.DashStyle)
            {
                case 2: case 3: return "StrokeStyle/$ID/Canned Dotted";
                case 4: case 5: case 6: case 7: case 8: case 9: case 10: case 11: return "StrokeStyle/$ID/Dashed";
                default: return "StrokeStyle/$ID/Solid";
            }
        }

        private static string ArrowHead(int a)
        {
            switch (a)
            {
                case 2: return "TriangleArrowHead";
                case 3: return "SimpleArrowHead";
                case 4: return "BarbedArrowHead";
                case 5: return "SquareSolidArrowHead";
                case 6: return "CircleSolidArrowHead";
                default: return "None";
            }
        }

        private static void WriteCorners(XmlWriter w, PubShape sh)
        {
            if (sh.Kind != PubShapeKind.RoundedRectangle) return;
            double frac = sh.Adjustments != null && sh.Adjustments.Length > 0 ? sh.Adjustments[0] : 0.16667;
            double r = Math.Max(0, frac) * Math.Min(sh.Width, sh.Height);
            foreach (string c in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
            {
                w.WriteAttributeString(c + "CornerOption", "RoundedCorner");
                w.WriteAttributeString(c + "CornerRadius", N(r));
            }
        }

        private static void WritePath(XmlWriter w, PubShape sh)
        {
            w.WriteStartElement("PathGeometry");
            w.WriteStartElement("GeometryPathType");
            bool open = sh.Kind == PubShapeKind.Line || (sh.Kind == PubShapeKind.Polygon && !sh.PathClosed);
            w.WriteAttributeString("PathOpen", open ? "true" : "false");
            w.WriteStartElement("PathPointArray");
            if (sh.Nodes != null && sh.Nodes.Count >= 2)
            {
                // Absolute page coordinates → inner (relative to the shape's top-left).
                double ox = sh.Left, oy = sh.Top;
                int i = 0;
                var anchors = new List<double[]>();   // x, y, lx, ly, rx, ry
                double[] pendingC1 = null, pendingC2 = null;
                foreach (var nd in sh.Nodes)
                {
                    double x = nd.X - ox, y = nd.Y - oy;
                    if (nd.IsControlPoint)
                    {
                        if (pendingC1 == null) pendingC1 = new[] { x, y }; else pendingC2 = new[] { x, y };
                        continue;
                    }
                    var a = new[] { x, y, x, y, x, y };
                    if (pendingC1 != null && anchors.Count > 0)
                    {
                        var prev = anchors[anchors.Count - 1];
                        prev[4] = pendingC1[0]; prev[5] = pendingC1[1];
                        if (pendingC2 != null) { a[2] = pendingC2[0]; a[3] = pendingC2[1]; }
                    }
                    pendingC1 = null; pendingC2 = null;
                    anchors.Add(a);
                    i++;
                }
                // Publisher repeats the first point to close a path; IDML closes implicitly.
                if (!open && anchors.Count > 2)
                {
                    var f = anchors[0]; var l = anchors[anchors.Count - 1];
                    if (Math.Abs(f[0] - l[0]) < 1e-6 && Math.Abs(f[1] - l[1]) < 1e-6) { f[2] = l[2]; f[3] = l[3]; anchors.RemoveAt(anchors.Count - 1); }
                }
                foreach (var a in anchors) PathPoint(w, a[0], a[1], a[2], a[3], a[4], a[5]);
            }
            else if (sh.Kind == PubShapeKind.Oval)
            {
                double W = sh.Width, H = sh.Height, k = 0.5522847498;
                double rx = W / 2, ry = H / 2, cx = W / 2, cy = H / 2;
                PathPoint(w, cx, 0, cx - rx * k, 0, cx + rx * k, 0);
                PathPoint(w, W, cy, W, cy - ry * k, W, cy + ry * k);
                PathPoint(w, cx, H, cx + rx * k, H, cx - rx * k, H);
                PathPoint(w, 0, cy, 0, cy + ry * k, 0, cy - ry * k);
            }
            else
            {
                double W = sh.Width, H = sh.Height;
                PathPoint(w, 0, 0, 0, 0, 0, 0);
                PathPoint(w, 0, H, 0, H, 0, H);
                PathPoint(w, W, H, W, H, W, H);
                PathPoint(w, W, 0, W, 0, W, 0);
            }
            w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
        }

        private static void PathPoint(XmlWriter w, double x, double y, double lx, double ly, double rx, double ry)
        {
            w.WriteStartElement("PathPointType");
            w.WriteAttributeString("Anchor", N(x) + " " + N(y));
            w.WriteAttributeString("LeftDirection", N(lx) + " " + N(ly));
            w.WriteAttributeString("RightDirection", N(rx) + " " + N(ry));
            w.WriteEndElement();
        }

        private void WriteTransparency(XmlWriter w, PubShape sh)
        {
            double opacity = 100;
            if (sh.Fill != null && sh.Fill.Visible && sh.Fill.Transparency > 0) opacity = (1 - sh.Fill.Transparency) * 100;
            bool shadow = sh.Shadow != null && sh.Shadow.Visible;
            if (opacity >= 99.999 && !shadow) return;
            w.WriteStartElement("TransparencySetting");
            if (opacity < 99.999)
            {
                w.WriteStartElement("BlendingSetting");
                w.WriteAttributeString("Opacity", N(opacity));
                w.WriteAttributeString("BlendMode", "Normal");
                w.WriteEndElement();
            }
            if (shadow)
            {
                w.WriteStartElement("DropShadowSetting");
                w.WriteAttributeString("Mode", "Drop");
                w.WriteAttributeString("XOffset", N(sh.Shadow.OffsetX));
                w.WriteAttributeString("YOffset", N(sh.Shadow.OffsetY));
                w.WriteAttributeString("Size", N(Math.Max(0, sh.Shadow.Blur)));
                w.WriteAttributeString("Opacity", N((1 - sh.Shadow.Transparency) * 100));
                w.WriteAttributeString("EffectColor", sh.Shadow.Color != null ? UseColor(sh.Shadow.Color) : "Color/Black");
                w.WriteAttributeString("BlendMode", "Multiply");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        private void WriteTextFramePreference(XmlWriter w, PubTextFrame tf)
        {
            WriteTextFramePreference(w, tf, false);
        }

        private void WriteTextFramePreference(XmlWriter w, PubTextFrame tf, bool ignoreWrap)
        {
            w.WriteStartElement("TextFramePreference");
            w.WriteAttributeString("IgnoreWrap", ignoreWrap ? "true" : "false");
            w.WriteAttributeString("TextColumnCount", N(Math.Max(1, tf.Columns)));
            w.WriteAttributeString("TextColumnGutter", N(tf.ColumnSpacing));
            w.WriteAttributeString("VerticalJustification", tf.VerticalAlignment == 1 ? "CenterAlign" : tf.VerticalAlignment == 2 ? "BottomAlign" : "TopAlign");
            // Publisher centres the glyphs in the first line box rather than hanging them from the
            // ascent; a fixed first-baseline distance reproduces that (and lets a 180 pt title fit a 172 pt cell).
            PubStory st;
            if (tf.StoryId != 0 && _doc.Stories.TryGetValue(tf.StoryId, out st) && st.Text.Length > 0)
            {
                w.WriteAttributeString("FirstBaselineOffset", "FixedHeight");
                w.WriteAttributeString("MinimumFirstBaselineOffset", N(FirstBaseline(st, null)));
            }
            else w.WriteAttributeString("FirstBaselineOffset", "AscentOffset");
            w.WriteAttributeString("AutoSizingType", "Off");
            w.WriteStartElement("Properties");
            w.WriteStartElement("InsetSpacing"); w.WriteAttributeString("type", "list");
            foreach (double v in new[] { tf.MarginTop, tf.MarginLeft, tf.MarginBottom, tf.MarginRight })
            {
                w.WriteStartElement("ListItem"); w.WriteAttributeString("type", "unit"); w.WriteString(N(v)); w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        private static void WriteWrap(XmlWriter w, PubShape sh)
        {
            // Publisher's default for every text box is "through" (3), which in practice wraps
            // nothing; and DesignCraft applies a frame's wrap to the frame's own text, so a wrap
            // on a small text frame would push its own text out. Only graphics get a wrap.
            if (sh.Wrap == null || sh.Wrap.Type == 0 || sh.Wrap.Type == 3 || sh.TextFrame != null) return;
            w.WriteStartElement("TextWrapPreference");
            w.WriteAttributeString("Inverse", "false");
            w.WriteAttributeString("ApplyToMasterPageOnly", "false");
            string side = sh.Wrap.Side == 1 ? "LeftSide" : sh.Wrap.Side == 2 ? "RightSide" : sh.Wrap.Side == 3 ? "LargestArea" : "BothSides";
            w.WriteAttributeString("TextWrapSide", side);
            string mode = sh.Wrap.Type == 1 ? "BoundingBoxTextWrap" : sh.Wrap.Type == 2 || sh.Wrap.Type == 3 ? "Contour" : "JumpObjectTextWrap";
            w.WriteAttributeString("TextWrapMode", mode);
            w.WriteStartElement("Properties");
            w.WriteStartElement("TextWrapOffset");
            w.WriteAttributeString("Top", N(sh.Wrap.Top));
            w.WriteAttributeString("Left", N(sh.Wrap.Left));
            w.WriteAttributeString("Bottom", N(sh.Wrap.Bottom));
            w.WriteAttributeString("Right", N(sh.Wrap.Right));
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        private void WriteGroup(XmlWriter w, PubShape sh, double dx, double dy)
        {
            if (sh.Children == null || sh.Children.Count == 0) return;
            w.WriteStartElement("Group");
            w.WriteAttributeString("Self", "ui" + sh.Id);
            w.WriteAttributeString("Name", string.IsNullOrEmpty(sh.Name) ? "$ID/" : sh.Name);
            w.WriteAttributeString("ItemLayer", LayerId);
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("Locked", "false");
            w.WriteAttributeString("ItemTransform", "1 0 0 1 " + N(dx) + " " + N(dy));
            w.WriteStartElement("Properties");
            var bounds = new PubShape { Kind = PubShapeKind.Rectangle, Width = sh.Width, Height = sh.Height };
            // Group geometry in group-inner coordinates = page coordinates (identity apart from the page offset).
            w.WriteStartElement("PathGeometry"); w.WriteStartElement("GeometryPathType"); w.WriteAttributeString("PathOpen", "false"); w.WriteStartElement("PathPointArray");
            PathPoint(w, sh.Left, sh.Top, sh.Left, sh.Top, sh.Left, sh.Top);
            PathPoint(w, sh.Left, sh.Top + sh.Height, sh.Left, sh.Top + sh.Height, sh.Left, sh.Top + sh.Height);
            PathPoint(w, sh.Left + sh.Width, sh.Top + sh.Height, sh.Left + sh.Width, sh.Top + sh.Height, sh.Left + sh.Width, sh.Top + sh.Height);
            PathPoint(w, sh.Left + sh.Width, sh.Top, sh.Left + sh.Width, sh.Top, sh.Left + sh.Width, sh.Top);
            w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndElement();
            var kids = new List<PubShape>(sh.Children);
            kids.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
            foreach (var c in kids) WriteItem(w, c, 0, 0);
            w.WriteEndElement();
        }

        private static string PictureSource(PubShape sh)
        {
            // The extracted PNG already has the crop applied and matches the frame; prefer the
            // original file only when it is linked, still exists and is not cropped.
            var p = sh.Picture;
            if (p == null) return null;
            bool cropped = p.CropLeft > 0.01 || p.CropTop > 0.01 || p.CropRight > 0.01 || p.CropBottom > 0.01;
            if (p.SourcePath != null && !cropped) return p.SourcePath;
            return p.ExtractedPath ?? p.SourcePath;
        }

        private void WritePictureItem(XmlWriter w, PubShape sh, double dx, double dy, string file)
        {
            // SaveAsPicture renders a rotated shape already rotated, inside the rotated bounding
            // box: place that image unrotated in the box instead of rotating it again. The same
            // holds for pictures we extracted (not for a linked original file, which is unrotated).
            bool rendered = sh.Kind == PubShapeKind.Raster || (sh.Picture != null && file != null && file == sh.Picture.ExtractedPath);
            if (rendered && (Math.Abs(sh.Rotation) > 1e-6 || sh.FlipHorizontal || sh.FlipVertical))
            {
                double r = sh.Rotation * Math.PI / 180, c = Math.Abs(Math.Cos(r)), s = Math.Abs(Math.Sin(r));
                double bw = sh.Width * c + sh.Height * s, bh = sh.Width * s + sh.Height * c;
                var box = new PubShape
                {
                    Id = sh.Id, Name = sh.Name, Kind = sh.Kind, ZOrder = sh.ZOrder, AltText = sh.AltText,
                    Left = sh.Left + sh.Width / 2 - bw / 2, Top = sh.Top + sh.Height / 2 - bh / 2, Width = bw, Height = bh,
                    Wrap = sh.Wrap, Shadow = sh.Shadow, Fill = null, Stroke = null, Picture = sh.Picture
                };
                sh = box;
            }
            w.WriteStartElement("Rectangle");
            WriteCommonAttributes(w, sh, "GraphicType", dx, dy);
            w.WriteAttributeString("FillColor", "Swatch/None");
            w.WriteAttributeString("FillTint", "-1");
            if (sh.Kind == PubShapeKind.Picture && sh.Stroke != null && sh.Stroke.Visible && sh.Stroke.Weight > 0 && sh.Stroke.Color != null)
            {
                w.WriteAttributeString("StrokeColor", UseColor(sh.Stroke.Color));
                w.WriteAttributeString("StrokeWeight", N(sh.Stroke.Weight));
                w.WriteAttributeString("StrokeType", StrokeType(sh.Stroke));
            }
            else { w.WriteAttributeString("StrokeColor", "Swatch/None"); w.WriteAttributeString("StrokeWeight", "0"); }
            w.WriteStartElement("Properties");
            WritePath(w, new PubShape { Kind = PubShapeKind.Rectangle, Width = sh.Width, Height = sh.Height });
            w.WriteEndElement();
            WriteTransparency(w, sh);
            WriteWrap(w, sh);

            if (file != null && File.Exists(file))
            {
                byte[] bytes = File.ReadAllBytes(file);
                int px = 0, py = 0;
                try
                {
                    using (var img = System.Drawing.Image.FromFile(file)) { px = img.Width; py = img.Height; }
                }
                catch { }
                double ppi = px > 0 && sh.Width > 0 ? px * 72.0 / sh.Width : 72;
                w.WriteStartElement("Image");
                w.WriteAttributeString("Self", "uimg" + sh.Id);
                w.WriteAttributeString("Name", "$ID/");
                w.WriteAttributeString("ItemTransform", "1 0 0 1 0 0");
                w.WriteAttributeString("ActualPpi", N(ppi) + " " + N(ppi));
                w.WriteAttributeString("EffectivePpi", N(ppi) + " " + N(ppi));
                w.WriteAttributeString("ImageTypeName", "$ID/" + ImageTypeName(file));
                w.WriteAttributeString("Space", "$ID/#Links_RGB");
                w.WriteStartElement("Properties");
                w.WriteStartElement("Contents");
                w.WriteCData(Base64Lines(bytes));
                w.WriteEndElement();
                w.WriteStartElement("GraphicBounds");
                w.WriteAttributeString("Left", "0"); w.WriteAttributeString("Top", "0");
                w.WriteAttributeString("Right", N(sh.Width)); w.WriteAttributeString("Bottom", N(sh.Height));
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteStartElement("Link");
                w.WriteAttributeString("Self", "ulnk" + sh.Id);
                w.WriteAttributeString("LinkResourceURI", "file:" + Path.GetFileName(file));
                w.WriteAttributeString("LinkResourceFormat", "$ID/" + ImageTypeName(file));
                w.WriteAttributeString("StoredState", "Embedded");
                w.WriteAttributeString("LinkClassID", "35906");
                w.WriteAttributeString("LinkClientID", "257");
                w.WriteAttributeString("LinkResourceModified", "false");
                w.WriteAttributeString("LinkObjectModified", "false");
                w.WriteAttributeString("ShowInUI", "true");
                w.WriteAttributeString("CanEmbed", "true");
                w.WriteAttributeString("CanUnembed", "true");
                w.WriteAttributeString("CanPackage", "true");
                w.WriteAttributeString("ImportPolicy", "NoAutoImport");
                w.WriteAttributeString("ExportPolicy", "NoAutoExport");
                w.WriteAttributeString("LinkResourceSize", "0~" + bytes.Length.ToString("x", Inv));
                w.WriteEndElement();
                w.WriteEndElement();
            }
            else _doc.Warnings.Add("Picture for shape '" + sh.Name + "' has no file; exported as an empty frame.");
            w.WriteEndElement();
        }

        private static string ImageTypeName(string file)
        {
            string ext = (Path.GetExtension(file) ?? "").ToLowerInvariant();
            switch (ext)
            {
                case ".jpg": case ".jpeg": return "JPEG";
                case ".gif": return "GIF";
                case ".tif": case ".tiff": return "TIFF";
                case ".bmp": return "BMP";
                default: return "Portable Network Graphics (PNG)";
            }
        }

        private static string Base64Lines(byte[] bytes)
        {
            string b64 = Convert.ToBase64String(bytes);
            var sb = new StringBuilder(b64.Length + b64.Length / 76 + 2);
            for (int i = 0; i < b64.Length; i += 76)
            {
                sb.Append(b64, i, Math.Min(76, b64.Length - i));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Tables

        private void WriteTableItem(XmlWriter w, PubShape sh, double dx, double dy)
        {
            var t = sh.Table;
            if (t == null) return;
            if (!_opt.TablesAsFrames)
            {
                // One text frame whose story holds the IDML table.
                w.WriteStartElement("TextFrame");
                WriteCommonAttributes(w, sh, "TextType", dx, dy);
                w.WriteAttributeString("ParentStory", "ust" + sh.Id);
                w.WriteAttributeString("PreviousTextFrame", "n");
                w.WriteAttributeString("NextTextFrame", "n");
                w.WriteAttributeString("FillColor", "Swatch/None");
                w.WriteAttributeString("StrokeColor", "Swatch/None");
                w.WriteAttributeString("StrokeWeight", "0");
                w.WriteStartElement("Properties");
                WritePath(w, new PubShape { Kind = PubShapeKind.Rectangle, Width = sh.Width, Height = sh.Height });
                w.WriteEndElement();
                WriteTextFramePreference(w, new PubTextFrame { Columns = 1 });
                w.WriteEndElement();
                return;
            }

            // Cell geometry from the column widths / row heights.
            var colX = new double[t.Columns + 1];
            for (int c = 0; c < t.Columns; c++) colX[c + 1] = colX[c] + (c < t.ColumnWidths.Count ? t.ColumnWidths[c] : 0);
            var rowY = new double[t.Rows + 1];
            for (int r = 0; r < t.Rows; r++) rowY[r + 1] = rowY[r] + (r < t.RowHeights.Count ? t.RowHeights[r] : 0);
            double sx = colX[t.Columns] > 0 ? sh.Width / colX[t.Columns] : 1, sy = rowY[t.Rows] > 0 ? sh.Height / rowY[t.Rows] : 1;

            w.WriteStartElement("Group");
            w.WriteAttributeString("Self", "ui" + sh.Id);
            w.WriteAttributeString("Name", string.IsNullOrEmpty(sh.Name) ? "Table" : sh.Name);
            w.WriteAttributeString("ItemLayer", LayerId);
            w.WriteAttributeString("Visible", "true");
            w.WriteAttributeString("Locked", "false");
            w.WriteAttributeString("ItemTransform", "1 0 0 1 " + N(dx) + " " + N(dy));
            foreach (var cell in t.Cells)
            {
                int c0 = Math.Max(0, cell.Column - 1), r0 = Math.Max(0, cell.Row - 1);
                int c1 = Math.Min(t.Columns, c0 + Math.Max(1, cell.ColumnSpan)), r1 = Math.Min(t.Rows, r0 + Math.Max(1, cell.RowSpan));
                double cw = (colX[c1] - colX[c0]) * sx, chh = (rowY[r1] - rowY[r0]) * sy;
                if (cell.Width > 0 && cell.ColumnSpan <= 1 && t.ColumnWidths.Count == 0) cw = cell.Width;
                // Publisher rows grow to fit their text and can be a point shorter than the text
                // they hold; give the frame the measured text height so nothing overflows.
                if (cell.Story != null && cell.Story.TextBoundHeight > 0)
                    chh = Math.Max(chh, cell.Story.TextBoundHeight + cell.MarginTop + cell.MarginBottom + OversetSlack(cell.Story) + 0.5);
                double border = Math.Max(Math.Max(cell.BorderTop, cell.BorderBottom), Math.Max(cell.BorderLeft, cell.BorderRight));
                var frame = new PubShape
                {
                    Id = unchecked(sh.Id * 1000 + cell.Row * 100 + cell.Column),
                    Name = sh.Name + " r" + cell.Row + "c" + cell.Column,
                    Kind = PubShapeKind.Rectangle,
                    Left = sh.Left + colX[c0] * sx, Top = sh.Top + rowY[r0] * sy, Width = cw, Height = chh,
                    Fill = cell.Fill != null ? new PubFill { Visible = true, Type = 1, Fore = cell.Fill } : null,
                    Stroke = border > 0 ? new PubStroke { Visible = true, Weight = border, Color = cell.BorderColor ?? new PubColor { Kind = PubColorKind.Rgb }, DashStyle = 1, CapStyle = 3, JoinStyle = 2 } : null,
                    TextFrame = new PubTextFrame { StoryId = cell.Story != null ? cell.Story.Id : 0, IsHead = true, Columns = 1, MarginLeft = cell.MarginLeft, MarginTop = cell.MarginTop, MarginRight = cell.MarginRight, MarginBottom = cell.MarginBottom, VerticalAlignment = cell.VerticalAlignment }
                };
                if (cell.Story != null && !_doc.Stories.ContainsKey(cell.Story.Id)) _doc.Stories[cell.Story.Id] = cell.Story;
                if (cell.Story == null || cell.Story.Text.Length == 0) frame.TextFrame = null;
                WriteItem(w, frame, 0, 0);
            }
            w.WriteEndElement();
        }

        private void WriteTableStory(XmlWriter w, StoryOut so)
        {
            var t = so.Table;
            w.WriteStartElement("ParagraphStyleRange");
            w.WriteAttributeString("AppliedParagraphStyle", "ParagraphStyle/$ID/NormalParagraphStyle");
            w.WriteStartElement("CharacterStyleRange");
            w.WriteAttributeString("AppliedCharacterStyle", NoCharStyle);
            w.WriteStartElement("Table");
            w.WriteAttributeString("Self", so.Self + "_t");
            w.WriteAttributeString("HeaderRowCount", "0");
            w.WriteAttributeString("FooterRowCount", "0");
            w.WriteAttributeString("BodyRowCount", N(t.Rows));
            w.WriteAttributeString("ColumnCount", N(t.Columns));
            w.WriteAttributeString("AppliedTableStyle", "TableStyle/$ID/[Basic Table]");
            w.WriteAttributeString("TableDirection", "LeftToRightDirection");
            for (int c = 0; c < t.Columns; c++)
            {
                w.WriteStartElement("Column");
                w.WriteAttributeString("Self", so.Self + "_c" + c);
                w.WriteAttributeString("Name", N(c));
                w.WriteAttributeString("SingleColumnWidth", N(c < t.ColumnWidths.Count ? t.ColumnWidths[c] : 72));
                w.WriteEndElement();
            }
            for (int r = 0; r < t.Rows; r++)
            {
                w.WriteStartElement("Row");
                w.WriteAttributeString("Self", so.Self + "_r" + r);
                w.WriteAttributeString("Name", N(r));
                w.WriteAttributeString("SingleRowHeight", N(r < t.RowHeights.Count ? t.RowHeights[r] : 20));
                w.WriteAttributeString("MinimumHeight", N(r < t.RowHeights.Count ? t.RowHeights[r] : 20));
                w.WriteEndElement();
            }
            foreach (var cell in t.Cells)
            {
                w.WriteStartElement("Cell");
                w.WriteAttributeString("Self", so.Self + "_" + (cell.Column - 1) + "_" + (cell.Row - 1));
                w.WriteAttributeString("Name", (cell.Column - 1) + ":" + (cell.Row - 1));
                w.WriteAttributeString("RowSpan", N(Math.Max(1, cell.RowSpan)));
                w.WriteAttributeString("ColumnSpan", N(Math.Max(1, cell.ColumnSpan)));
                w.WriteAttributeString("AppliedCellStyle", "CellStyle/$ID/[None]");
                if (cell.Fill != null) { w.WriteAttributeString("FillColor", UseColor(cell.Fill)); w.WriteAttributeString("FillTint", "-1"); }
                w.WriteAttributeString("VerticalJustification", cell.VerticalAlignment == 1 ? "CenterAlign" : cell.VerticalAlignment == 2 ? "BottomAlign" : "TopAlign");
                w.WriteAttributeString("TopEdgeStrokeWeight", N(cell.BorderTop));
                w.WriteAttributeString("BottomEdgeStrokeWeight", N(cell.BorderBottom));
                w.WriteAttributeString("LeftEdgeStrokeWeight", N(cell.BorderLeft));
                w.WriteAttributeString("RightEdgeStrokeWeight", N(cell.BorderRight));
                if (cell.Story != null) WriteStoryBody(w, cell.Story);
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        // --------------------------------------------------------------- Stories

        private void WriteStory(XmlWriter w, StoryOut so)
        {
            w.WriteStartElement("Story");
            w.WriteAttributeString("Self", so.Self);
            w.WriteAttributeString("AppliedTOCStyle", "n");
            w.WriteAttributeString("TrackChanges", "false");
            w.WriteAttributeString("StoryTitle", "$ID/");
            w.WriteAttributeString("AppliedNamedGrid", "n");
            w.WriteStartElement("StoryPreference");
            w.WriteAttributeString("OpticalMarginAlignment", "false");
            w.WriteAttributeString("OpticalMarginSize", "12");
            w.WriteAttributeString("FrameType", "TextFrameType");
            w.WriteAttributeString("StoryOrientation", "Horizontal");
            w.WriteAttributeString("StoryDirection", "LeftToRightDirection");
            w.WriteEndElement();
            w.WriteStartElement("InCopyExportOption");
            w.WriteAttributeString("IncludeGraphicProxies", "true");
            w.WriteAttributeString("IncludeAllResources", "false");
            w.WriteEndElement();
            if (so.Table != null) WriteTableStory(w, so);
            else WriteStoryBody(w, so.Story);
            w.WriteEndElement();
        }

        private void WriteStoryBody(XmlWriter w, PubStory st)
        {
            string text = st.Text ?? "";
            var paras = new List<PubParagraph>(st.Paragraphs);
            if (paras.Count == 0) paras.Add(new PubParagraph { Start = 0, End = text.Length, Format = new PubParagraphFormat { StyleName = "Normal", ListType = 255 } });
            paras.Sort((a, b) => a.Start.CompareTo(b.Start));

            for (int pi = 0; pi < paras.Count; pi++)
            {
                var para = paras[pi];
                int pStart = Math.Max(0, Math.Min(para.Start, text.Length));
                int pEnd = pi + 1 < paras.Count ? Math.Max(pStart, Math.Min(paras[pi + 1].Start, text.Length)) : text.Length;
                bool last = pi == paras.Count - 1;

                PubStyle style = null;
                string styleName = para.Format != null ? para.Format.StyleName : null;
                if (!string.IsNullOrEmpty(styleName)) _styleByName.TryGetValue(styleName, out style);
                PubFont styleFont = style != null ? style.Font : null;
                double size = FirstRunSize(st, pStart, pEnd, styleFont);

                w.WriteStartElement("ParagraphStyleRange");
                w.WriteAttributeString("AppliedParagraphStyle", style != null ? StyleSelf(style.Name) : "ParagraphStyle/$ID/NormalParagraphStyle");
                if (para.Format != null) WriteParaAttributes(w, para.Format, style != null ? style.Paragraph : null, size);
                if (para.Format != null && (style == null || LeadingDiffers(para.Format, style.Paragraph)))
                {
                    w.WriteStartElement("Properties");
                    WriteLeadingProp(w, para.Format, size);
                    if (style == null || !SameTabs(para.Format, style.Paragraph)) WriteTabList(w, para.Format);
                    w.WriteEndElement();
                }

                // Character ranges inside this paragraph (runs clipped to [pStart, pEnd)).
                var pieces = new List<PubRun>();
                foreach (var r in st.Runs)
                {
                    int a = Math.Max(r.Start, pStart), b = Math.Min(r.Start + r.Length, pEnd);
                    if (b > a) pieces.Add(new PubRun { Start = a, Length = b - a, Font = r.Font });
                }
                if (pieces.Count == 0) pieces.Add(new PubRun { Start = pStart, Length = pEnd - pStart, Font = styleFont });
                pieces.Sort((a, b) => a.Start.CompareTo(b.Start));

                for (int k = 0; k < pieces.Count; k++)
                {
                    var piece = pieces[k];
                    bool lastPiece = k == pieces.Count - 1;
                    WriteCharRange(w, st, piece, styleFont, text, lastPiece && !last);
                }
                w.WriteEndElement();
            }
        }

        private static double FirstRunSize(PubStory st, int start, int end, PubFont fallback)
        {
            foreach (var r in st.Runs) if (r.Start + r.Length > start && r.Start < end && r.Font != null && r.Font.Size > 0) return r.Font.Size;
            return fallback != null && fallback.Size > 0 ? fallback.Size : 10;
        }

        private static bool LeadingDiffers(PubParagraphFormat a, PubParagraphFormat b)
        {
            if (b == null || a.MeasuredLeading > 0) return true;   // measured leading is always written explicitly
            return a.LineSpacingRule != b.LineSpacingRule || Math.Abs(a.LineSpacing - b.LineSpacing) > 1e-6 || !SameTabs(a, b);
        }

        /// <summary>
        /// Where Publisher puts the first baseline (measured): the line box is the leading and the
        /// baseline divides it in the font's ascent:descent ratio, so glyphs may overhang the box.
        /// </summary>
        private static double FirstBaseline(PubStory st, PubFont styleFont)
        {
            PubFont f = st.Runs.Count > 0 ? st.Runs[0].Font : styleFont;
            double size = f != null && f.Size > 0 ? f.Size : 10;
            double ascent, descent;
            Metrics(f, out ascent, out descent);
            PubParagraphFormat pf = st.Paragraphs.Count > 0 ? st.Paragraphs[0].Format : null;
            double leading = LeadingOf(pf, size);
            return Math.Max(1, leading * ascent / (ascent + descent));
        }

        /// <summary>Ascent and descent as fractions of the em: the installed font's metrics, else typical values.</summary>
        private static readonly Dictionary<string, double[]> MetricsCache = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        private static void Metrics(PubFont f, out double ascent, out double descent)
        {
            ascent = 0.9; descent = 0.22;
            if (f == null || string.IsNullOrEmpty(f.Name)) return;
            double[] m;
            if (!MetricsCache.TryGetValue(f.Name, out m))
            {
                m = new[] { ascent, descent };
                try
                {
                    using (var fam = new System.Drawing.FontFamily(f.Name))
                    {
                        var style = System.Drawing.FontStyle.Regular;
                        if (!fam.IsStyleAvailable(style)) style = System.Drawing.FontStyle.Bold;
                        double em = fam.GetEmHeight(style);
                        if (em > 0) { m[0] = fam.GetCellAscent(style) / em; m[1] = fam.GetCellDescent(style) / em; }
                    }
                }
                catch { }
                MetricsCache[f.Name] = m;
            }
            ascent = m[0]; descent = m[1];
        }

        /// <summary>
        /// DesignCraft (and InDesign) only show a line whose baseline + descent fits the frame;
        /// Publisher lets the descent overhang the line box. The extra height a frame needs so
        /// text that fits in Publisher also fits elsewhere: max over runs of
        /// descent·size − leading·descent/(ascent+descent).
        /// </summary>
        private static double OversetSlack(PubStory st)
        {
            double slack = 0;
            foreach (var r in st.Runs)
            {
                if (r.Font == null || r.Font.Size <= 0) continue;
                PubParagraphFormat pf = null;
                foreach (var p in st.Paragraphs) if (r.Start >= p.Start && r.Start < Math.Max(p.End, p.Start + 1)) { pf = p.Format; break; }
                double asc, desc;
                Metrics(r.Font, out asc, out desc);
                double leading = LeadingOf(pf, r.Font.Size);
                slack = Math.Max(slack, desc * r.Font.Size - leading * desc / (asc + desc));
            }
            return slack > 0.25 ? slack + 0.5 : 0;
        }

        /// <summary>
        /// Extra height a text frame of <paramref name="height"/> needs so that its last line's
        /// baseline + descent fits: Publisher shows a line whose box merely starts inside the
        /// frame. Uses the text height Publisher measured and the first-baseline rule above.
        /// </summary>
        private static double FrameSlack(PubStory st, double height, double insets)
        {
            if (st.TextBoundHeight <= 0 || st.Runs.Count == 0) return 0;
            double maxDesc = 0, pitch = 0;
            foreach (var r in st.Runs)
            {
                if (r.Font == null || r.Font.Size <= 0) continue;
                double asc, desc;
                Metrics(r.Font, out asc, out desc);
                maxDesc = Math.Max(maxDesc, desc * r.Font.Size);
                if (pitch == 0)
                {
                    PubParagraphFormat pf = st.Paragraphs.Count > 0 ? st.Paragraphs[0].Format : null;
                    pitch = LeadingOf(pf, r.Font.Size);
                    // the part of the first line box above the baseline is pitch·asc/(asc+desc)
                    pitch -= pitch * asc / (asc + desc);   // now: below-baseline part of a line box
                }
            }
            if (maxDesc <= 0) return 0;
            double needed = st.TextBoundHeight - pitch + maxDesc + insets;
            double slack = needed - height;
            return slack > 0.25 ? slack + 0.5 : 0;
        }

        private static bool SameTabs(PubParagraphFormat a, PubParagraphFormat b)
        {
            if (b == null) return a.Tabs.Count == 0;
            if (a.Tabs.Count != b.Tabs.Count) return false;
            for (int i = 0; i < a.Tabs.Count; i++)
                if (Math.Abs(a.Tabs[i].Position - b.Tabs[i].Position) > 1e-6 || a.Tabs[i].Alignment != b.Tabs[i].Alignment || a.Tabs[i].Leader != b.Tabs[i].Leader) return false;
            return true;
        }

        /// <summary>Writes one CharacterStyleRange, splitting around hyperlinks and page-number fields.</summary>
        private void WriteCharRange(XmlWriter w, PubStory st, PubRun piece, PubFont styleFont, string text, bool breakAfter)
        {
            int start = piece.Start, end = piece.Start + piece.Length;
            // Cut points: hyperlink boundaries and field positions.
            var cuts = new SortedSet<int> { start, end };
            foreach (var h in st.Hyperlinks) { if (h.Start > start && h.Start < end) cuts.Add(h.Start); if (h.End > start && h.End < end) cuts.Add(h.End); }
            foreach (var f in st.Fields) { if (f.Start > start && f.Start < end) cuts.Add(f.Start); int fe = f.Start + f.Length; if (fe > start && fe < end) cuts.Add(fe); }
            var pts = new List<int>(cuts);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                int a = pts[i], b = pts[i + 1];
                PubHyperlink link = null;
                foreach (var h in st.Hyperlinks) if (a >= h.Start && b <= h.End) { link = h; break; }
                PubField field = null;
                foreach (var f in st.Fields) if (a >= f.Start && b <= f.Start + f.Length) { field = f; break; }
                bool isLast = i + 2 == pts.Count;

                if (link != null)
                {
                    int key = _hyperlinks.Count + 1;
                    var ho = new HyperlinkOut { Key = key, SourceSelf = "uhts" + key, DestSelf = "HyperlinkURLDestination/" + Esc(link.Address) + "_" + key, Name = Safe(text, a, b), Url = link.Address };
                    _hyperlinks.Add(ho);
                    w.WriteStartElement("HyperlinkTextSource");
                    w.WriteAttributeString("Self", ho.SourceSelf);
                    w.WriteAttributeString("Name", ho.Name);
                    w.WriteAttributeString("Hidden", "false");
                }
                w.WriteStartElement("CharacterStyleRange");
                w.WriteAttributeString("AppliedCharacterStyle", NoCharStyle);
                if (piece.Font != null) WriteCharAttributes(w, piece.Font, styleFont);
                if (field != null && field.Type == 1)
                {
                    w.WriteProcessingInstruction("ACE", "18");
                }
                else if (field != null && field.Type != 8)
                {
                    // Every other field (next/previous page, date, mail merge, hyperlink fields):
                    // its Result is what the reader sees; Publisher's '#' placeholder is dropped.
                    string result = field.Result ?? "";
                    if (result.Length > 0) WriteContent(w, result);
                    else if (Safe(text, a, b) != "#") WriteContent(w, Safe(text, a, b));
                }
                else WriteContent(w, Safe(text, a, b));
                if (isLast && breakAfter) { w.WriteStartElement("Br"); w.WriteEndElement(); }
                w.WriteEndElement();
                if (link != null) w.WriteEndElement();
            }
        }

        private static void WriteContent(XmlWriter w, string s)
        {
            if (s.Length == 0) return;
            // Split on forced line breaks (Shift+Enter) and tabs so each piece is a valid Content/Tab element.
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\n' || ch == '\u000B' || ch == (char)0x2028)
                {
                    if (sb.Length > 0) { Content(w, sb.ToString()); sb.Length = 0; }
                    Content(w, ((char)0x2028).ToString());
                }
                else if (ch == '\t') { if (sb.Length > 0) { Content(w, sb.ToString()); sb.Length = 0; } w.WriteStartElement("Tab"); w.WriteEndElement(); }
                else if (ch == '\r') { /* paragraph marks are expressed with Br */ }
                else if (ch < ' ') { /* other control characters are dropped */ }
                else sb.Append(ch);
            }
            if (sb.Length > 0) Content(w, sb.ToString());
        }

        private static void Content(XmlWriter w, string s)
        {
            w.WriteStartElement("Content");
            w.WriteString(s);
            w.WriteEndElement();
        }

        private static string Safe(string text, int a, int b)
        {
            a = Math.Max(0, Math.Min(a, text.Length)); b = Math.Max(a, Math.Min(b, text.Length));
            return text.Substring(a, b - a);
        }

        // --------------------------------------------------------------- helpers

        private static int Lerp(int a, int b, double t) { return (int)Math.Round(a + (b - a) * Math.Max(0, Math.Min(1, t))); }

        /// <summary>IDML id escaping as InDesign does it: '%' → %25, ':' → %3a (and we drop quotes/control chars).</summary>
        private static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c == '%') sb.Append("%25");
                else if (c == ':') sb.Append("%3a");
                else if (c == '"' || c == '<' || c == '>' || c == '&' || c < ' ') sb.Append('_');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string N(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
            v = Math.Round(v, 4);
            if (Math.Abs(v) < 1e-9) v = 0;   // avoid "-0"
            return v.ToString("0.####", Inv);
        }

        // 2-D affine matrices as [a b c d e f]: x' = a x + c y + e; y' = b x + d y + f.
        private static double[] Identity() { return new[] { 1.0, 0, 0, 1, 0, 0 }; }
        private static double[] Translate(double x, double y) { return new[] { 1.0, 0, 0, 1, x, y }; }
        private static double[] Scale(double x, double y) { return new[] { x, 0, 0, y, 0, 0 }; }
        private static double[] Rotate(double degrees)
        {
            double r = degrees * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            return new[] { c, s, -s, c, 0, 0 };
        }
        /// <summary>m1 · m2 (apply m2 first, then m1).</summary>
        private static double[] Mul(double[] m1, double[] m2)
        {
            return new[]
            {
                m1[0] * m2[0] + m1[2] * m2[1],
                m1[1] * m2[0] + m1[3] * m2[1],
                m1[0] * m2[2] + m1[2] * m2[3],
                m1[1] * m2[2] + m1[3] * m2[3],
                m1[0] * m2[4] + m1[2] * m2[5] + m1[4],
                m1[1] * m2[4] + m1[3] * m2[5] + m1[5]
            };
        }
        private static string Matrix(double[] m) { return N(m[0]) + " " + N(m[1]) + " " + N(m[2]) + " " + N(m[3]) + " " + N(m[4]) + " " + N(m[5]); }
    }
}
