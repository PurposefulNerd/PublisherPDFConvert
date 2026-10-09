using System.Collections.Generic;

// A plain in-memory picture of a Publisher publication: everything the
// automation object model reports that an editable export needs, and nothing
// COM-specific. PublisherReader fills it; IdmlWriter consumes it.
//
// Units: points, y down, origin at the top-left of the page the item sits on
// (exactly what Publisher reports). Colours are kept as the user chose them
// (RGB, scheme index or CMYK) so the writer can emit proper swatches.
// Booleans from MsoTriState are already converted (-1 -> true).

namespace PublisherToPdf.Model
{
    public enum PubShapeKind
    {
        Rectangle,
        RoundedRectangle,
        Oval,
        Line,
        Polygon,     // freeform / polyline / any other autoshape, as path nodes
        Group,
        TextFrame,
        Picture,
        Table,
        Raster       // no vector equivalent: exported as an image of the shape
    }

    public enum PubColorKind { Rgb, Scheme, Cmyk }

    public sealed class PubColor
    {
        public PubColorKind Kind;
        public int R, G, B;          // always filled (Publisher resolves scheme/CMYK to RGB too)
        public int SchemeIndex;      // PbSchemeColorIndex when Kind == Scheme
        public int C, M, Y, K;       // 0..100 when Kind == Cmyk
        public double Tint;          // TintAndShade, -1..1 (0 = none)
        public double Transparency;  // 0..1

        public string Hex { get { return string.Format("{0:X2}{1:X2}{2:X2}", R, G, B); } }
    }

    public sealed class PubFill
    {
        public bool Visible;
        public int Type;             // MsoFillType: 1 solid, 2 patterned, 3 gradient, 4 textured, 5 background, 6 picture
        public PubColor Fore;
        public PubColor Back;
        public double Transparency;  // 0..1
        public int GradientStyle;    // MsoGradientStyle (1 horizontal, 2 vertical, 3 diagonal up, 4 diagonal down, 5 from corner, 6 from title, 7 from center)
        public int GradientVariant;
        public double GradientAngle;
        public int GradientColorType; // 1 one colour, 2 two colours, 3 preset
    }

    public sealed class PubStroke
    {
        public bool Visible;
        public double Weight;        // points
        public PubColor Color;
        public int DashStyle;        // MsoLineDashStyle (1 solid, 2 square dot, 3 round dot, 4 dash, 5 dash dot, 6 dash dot dot, 7 long dash, 8 long dash dot, ...)
        public int LineStyle;        // MsoLineStyle (1 single, 2 thin-thin, 3 thin-thick, 4 thick-thin, 5 thick-between-thin)
        public int CapStyle;         // MsoLineCapStyle (1 round, 2 square, 3 flat)
        public int JoinStyle;        // MsoLineJoinStyle (1 bevel, 2 miter, 3 round)
        public int BeginArrow;       // MsoArrowheadStyle (1 none, 2 triangle, 3 open, 4 stealth, 5 diamond, 6 oval)
        public int EndArrow;
        public double Transparency;
    }

    public sealed class PubShadow
    {
        public bool Visible;
        public double Blur;
        public double OffsetX, OffsetY;
        public PubColor Color;
        public double Transparency;
    }

    public sealed class PubWrap
    {
        public int Type;             // PbWrapType: 0 none, 1 square, 2 tight, 3 through, 4 top and bottom
        public int Side;             // PbWrapSideType: 0 both, 1 left, 2 right, 3 larger, 4 neither
        public bool DistanceAuto;
        public double Left, Top, Right, Bottom;
    }

    public sealed class PubFont
    {
        public string Name;
        public double Size;
        public bool Bold, Italic;
        public int Underline;        // PbUnderlineType (0 none, 1 single, 2 words only, 3 double, ...)
        public PubColor Color;
        public bool AllCaps, SmallCaps, StrikeThrough, Superscript, Subscript;
        public double Tracking;      // percent, 100 = normal
        public double Kerning;
        public double Scaling;       // percent, 100 = normal
        public double Position;      // baseline shift, points

        public string Key()
        {
            return string.Join("|", new object[]
            {
                Name, Size, Bold, Italic, Underline, Color == null ? "" : Color.Hex, AllCaps, SmallCaps,
                StrikeThrough, Superscript, Subscript, Tracking, Scaling, Position
            });
        }
    }

    public sealed class PubTabStop
    {
        public double Position;
        public int Alignment;        // PbTabAlignmentType: 0 leading, 1 center, 2 trailing, 3 decimal
        public int Leader;           // PbTabLeaderType: 0 none, 1 dot, 2 dashes, 3 line, 5 bullet
    }

    public sealed class PubParagraphFormat
    {
        public string StyleName;
        public int Alignment;        // PbParagraphAlignmentType: 0 left, 1 center, 2 right, 3 inter-word, 6 justified, ...
        public double LeftIndent, RightIndent, FirstLineIndent;
        public double SpaceBefore, SpaceAfter;
        public double LineSpacing;   // points (rule 4) or multiplier (rules 0/1/2/5)
        public int LineSpacingRule;  // PbLineSpacingRule: 0 single, 1 1.5, 2 double, 4 exactly, 5 multiple
        public double MeasuredLeading;   // line pitch in points as Publisher laid it out (0 = unknown, e.g. styles)
        public int ListType;         // PbListType: 23 bullet, 255 none, otherwise numbered
        public string BulletText;
        public double ListIndent;
        public int ListNumberStart;
        public bool KeepLinesTogether, KeepWithNext, WidowControl;
        public List<PubTabStop> Tabs = new List<PubTabStop>();
        public int DropCapLines, DropCapSpan;   // 0 = no drop cap
    }

    public sealed class PubStyle
    {
        public string Name;
        public string BaseStyle;     // "" or "[no style]" when none
        public string NextStyle;
        public PubFont Font;
        public PubParagraphFormat Paragraph;
    }

    public sealed class PubParagraph
    {
        public int Start, End;       // character offsets in Story.Text; End is exclusive and includes the '\r'
        public PubParagraphFormat Format;
    }

    public sealed class PubRun
    {
        public int Start, Length;    // 0-based character offsets in Story.Text
        public PubFont Font;
    }

    public sealed class PubHyperlink
    {
        public int Start, End;
        public string Address;
    }

    public sealed class PubField
    {
        public int Start;            // offset of the field's placeholder character
        public int Length;
        public int Type;             // PbFieldType: 1 page number, 2 next page, 3 previous page, 4 date/time, 5 mail merge, ...
        public string Result;
    }

    public sealed class PubStory
    {
        public int Id;               // ID of the head text frame (or synthetic for table cells)
        public string Text = "";     // paragraphs separated by '\r'
        public List<PubParagraph> Paragraphs = new List<PubParagraph>();
        public List<PubRun> Runs = new List<PubRun>();
        public List<PubHyperlink> Hyperlinks = new List<PubHyperlink>();
        public List<PubField> Fields = new List<PubField>();
        public List<int> FrameIds = new List<int>();   // thread order, head first
        public double TextBoundHeight;                 // height of the laid-out text in Publisher, points (0 = unknown)
    }

    public sealed class PubTextFrame
    {
        public int StoryId;
        public bool IsHead;          // first frame of its story (the story text lives here)
        public int Columns;
        public double ColumnSpacing;
        public double MarginLeft, MarginTop, MarginRight, MarginBottom;
        public int VerticalAlignment;   // PbVerticalTextAlignmentType: 0 top, 1 center, 2 bottom
        public int NextShapeId, PreviousShapeId;   // 0 = none
        public int Orientation;      // PbTextOrientation: 1 horizontal, 2 vertical East Asian, 256 right-to-left
    }

    public sealed class PubPicture
    {
        public string OriginalName;  // PictureFormat.Filename (name only for embedded pictures, full path for linked)
        public string SourcePath;    // full path of a linked picture that still exists, else null
        public string ExtractedPath; // PNG written by SaveAsPicture (always attempted)
        public bool Linked;
        public double CropLeft, CropTop, CropRight, CropBottom;   // points of the source image
        public double OriginalWidth, OriginalHeight;              // points, as placed before crop/scale (0 if unknown)
        public int ImageFormat;      // PbImageFormat
    }

    public sealed class PubCell
    {
        public int Row, Column;      // 1-based
        public int RowSpan, ColumnSpan;
        public double Width, Height; // points
        public PubStory Story;
        public PubColor Fill;
        public int VerticalAlignment;
        public double BorderTop, BorderBottom, BorderLeft, BorderRight;   // weights, 0 = none
        public PubColor BorderColor;
        public double MarginLeft, MarginTop, MarginRight, MarginBottom;
    }

    public sealed class PubTable
    {
        public int Rows, Columns;
        public List<double> ColumnWidths = new List<double>();
        public List<double> RowHeights = new List<double>();
        public List<PubCell> Cells = new List<PubCell>();   // one entry per merge origin
    }

    public sealed class PubPathNode
    {
        public double X, Y;          // absolute page coordinates
        public int SegmentType;      // MsoSegmentType: 0 line, 1 curve (this node ends a Bézier whose two control points precede it)
        public int EditingType;
        public bool IsControlPoint;
    }

    public sealed class PubShape
    {
        public int Id;
        public string Name;
        public PubShapeKind Kind;
        public int PublisherType;    // PbShapeType
        public int AutoShapeType;    // MsoAutoShapeType, -2 when not an autoshape
        public double Left, Top, Width, Height;   // unrotated bounds
        public double Rotation;      // degrees, clockwise
        public bool FlipHorizontal, FlipVertical;
        public int ZOrder;
        public string AltText;
        public string HyperlinkAddress;
        public PubFill Fill;
        public PubStroke Stroke;
        public PubShadow Shadow;
        public PubWrap Wrap;
        public double[] Adjustments;
        public bool PathClosed = true;
        public List<PubPathNode> Nodes;            // Polygon / Line
        public List<PubShape> Children;            // Group
        public PubTextFrame TextFrame;             // TextFrame (and autoshapes that carry text)
        public PubPicture Picture;                 // Picture
        public PubTable Table;                     // Table
        public string RasterPath;                  // Raster: PNG of the shape
        public string RasterReason;
    }

    public sealed class PubPage
    {
        public int Id;
        public int Index;            // 1-based within Pages or MasterPages
        public string Name;
        public string PageNumber;    // formatted, as Publisher shows it
        public int PageType;         // PbPageType: 1 left, 2 right, 3 scratch, 4 master
        public double Width, Height;
        public int MasterId;
        public string MasterName;
        public bool IsTwoPageMaster;
        public bool IgnoreMaster;
        public double MarginLeft, MarginTop, MarginRight, MarginBottom;
        public int Columns;
        public double ColumnGutter;
        public PubFill Background;
        public List<PubShape> Shapes = new List<PubShape>();
    }

    public sealed class PubSection
    {
        public int StartPageIndex;   // 1-based
        public int PageNumberStart;
        public int PageNumberFormat; // PbPageNumberFormat: 0 arabic, 1 UC roman, 2 LC roman, 3 UC letter, 4 LC letter
        public bool ContinueFromPrevious;
    }

    public sealed class PubDocument
    {
        public string SourcePath;
        public string PublisherVersion;
        public double PageWidth, PageHeight;
        public int Orientation;      // 1 portrait, 2 landscape
        public int Layout;           // PbPublicationLayout (2 = book -> facing pages)
        public bool FacingPages;
        public double MarginLeft, MarginTop, MarginRight, MarginBottom;
        public int Columns;
        public double ColumnGutter;
        public bool MirrorGuides;
        public double BaselineSpacing, BaselineOffset;
        public string SchemeName;
        public PubColor[] SchemeColors = new PubColor[9];   // index = PbSchemeColorIndex (1..8), 0 unused
        public List<PubStyle> Styles = new List<PubStyle>();
        public List<PubSection> Sections = new List<PubSection>();
        public List<PubPage> Masters = new List<PubPage>();
        public List<PubPage> Pages = new List<PubPage>();
        public List<PubShape> ScratchShapes = new List<PubShape>();
        [System.Web.Script.Serialization.ScriptIgnore]
        public Dictionary<int, PubStory> Stories = new Dictionary<int, PubStory>();
        /// <summary>Stories as a list, for the JSON model dump (the serializer rejects int-keyed dictionaries).</summary>
        public List<PubStory> StoryList { get { return new List<PubStory>(Stories.Values); } }
        public List<string> Warnings = new List<string>();
    }
}
