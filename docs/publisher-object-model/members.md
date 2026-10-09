# Publisher object model: interfaces and members

## Adjustments
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Object {get;set}
- prop Parent : Object {get}

## AdvancedPrintOptions
- prop AllowBleeds : Boolean {get;set}
- prop Application : Application {get}
- prop BackSideInsertFaceUp : Boolean {get;set}
- prop GraphicsResolution : PbPrintGraphics {get;set}
- prop HorizontalFlip : Boolean {get;set}
- prop InksToPrint : PbInksToPrint {get;set}
- prop IsPostscriptPrinter : Boolean {get}
- prop ManualFeedAlign : PbPlacementType {get;set}
- prop ManualFeedDirection : PbOrientationType {get;set}
- prop NegativeImage : Boolean {get;set}
- prop PageRotated : Boolean {get;set}
- prop Parent : Object {get}
- prop PrintablePlates : PrintablePlates {get}
- prop PrintableRect : PrintableRect {get}
- prop PrintBlankPlates : Boolean {get;set}
- prop PrintBleedMarks : Boolean {get;set}
- prop PrintCMYKByDefault : Boolean {get;set}
- prop PrintColorBars : Boolean {get;set}
- prop PrintCropMarks : Boolean {get;set}
- prop PrintDensityBars : Boolean {get;set}
- prop PrintJobInformation : Boolean {get;set}
- prop PrintMode : PbPrintMode {get;set}
- prop PrintRegistrationMarks : Boolean {get;set}
- prop Resolution : String {get;set}
- prop UseCustomHalftone : Boolean {get;set}
- prop UseOnlyPublicationFonts : Boolean {get;set}
- prop VerticalFlip : Boolean {get;set}

## Application

## Attachment
- prop Name : String {get}
- method Delete() : Void

## Attachments
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : Attachment {get}
- prop Parent : Object {get}
- method Add(String Filename) : Attachment
- method ClearAll() : Void
- method GetEnumerator() : IEnumerator

## BorderArt
- prop Application : Application {get}
- prop Name : String {get}
- prop Parent : Object {get}

## BorderArtFormat
- prop Application : Application {get}
- prop Color : ColorFormat {get;set}
- prop Exists : Boolean {get}
- prop Name : String {get;set}
- prop Parent : Object {get}
- prop StretchPictures : Boolean {get;set}
- prop Weight : Object {get;set}
- method Delete() : Void
- method RevertToDefaultWeight() : Void
- method RevertToOriginalColor() : Void
- method Set(Object BorderArtName) : Void

## BorderArts
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : BorderArt {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## BuildingBlock
- prop Category : String {get;set}
- prop Description : String {get;set}
- prop Gallery : PbBuildingBlockGallery {get;set}
- prop Keywords : String {get;set}
- prop Name : String {get;set}
- prop ShowInGallery : Boolean {get;set}
- prop Type : PbBuildingBlockType {get}
- method Delete() : Void

## BuildingBlocks
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : BuildingBlock {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## CalloutFormat
- prop Accent : MsoTriState {get;set}
- prop Angle : MsoCalloutAngleType {get;set}
- prop Application : Application {get}
- prop AutoAttach : MsoTriState {get;set}
- prop AutoLength : MsoTriState {get}
- prop Border : MsoTriState {get;set}
- prop Drop : Object {get}
- prop DropType : MsoCalloutDropType {get}
- prop Gap : Object {get;set}
- prop Length : Object {get}
- prop Parent : Object {get}
- prop Type : MsoCalloutType {get;set}
- method AutomaticLength() : Void
- method CustomDrop(Object Drop) : Void
- method CustomLength(Object Length) : Void
- method PresetDrop(MsoCalloutDropType DropType) : Void

## CaptionStyle
- prop Application : Application {get}
- prop Name : String {get}
- prop Parent : Object {get}

## CaptionStyles
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : CaptionStyle {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## CatalogMergeShapes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop HorizontalRepeat : Int32 {get}
- prop Item[Object Index] : Shape {get}
- prop Parent : Object {get}
- prop VerticalRepeat : Int32 {get}
- method GetEnumerator() : IEnumerator
- method Range(Object Index) : ShapeRange

## Cell
- prop Application : Application {get}
- prop BorderBottom : CellBorder {get}
- prop BorderDiagonal : CellBorder {get}
- prop BorderLeft : CellBorder {get}
- prop BorderRight : CellBorder {get}
- prop BorderTop : CellBorder {get}
- prop CellTextOrientation : PbTextOrientation {get;set}
- prop Column : Int32 {get}
- prop Diagonal : PbCellDiagonalType {get;set}
- prop Fill : FillFormat {get}
- prop HasText : Boolean {get}
- prop Height : Int32 {get}
- prop MarginBottom : Object {get;set}
- prop MarginLeft : Object {get;set}
- prop MarginRight : Object {get;set}
- prop MarginTop : Object {get;set}
- prop Parent : Object {get}
- prop Row : Int32 {get}
- prop Selected : Boolean {get}
- prop TextRange : TextRange {get}
- prop VerticalTextAlignment : PbVerticalTextAlignmentType {get;set}
- prop Width : Int32 {get}
- method Merge(Cell MergeTo) : Void
- method Select() : Void
- method Split() : CellRange

## CellBorder
- prop Application : Application {get}
- prop Color : ColorFormat {get}
- prop Parent : Object {get}
- prop Weight : Object {get;set}

## CellRange
- prop Application : Application {get}
- prop Column : Int32 {get}
- prop Count : Int32 {get}
- prop Height : Int32 {get}
- prop Item[Int32 Index] : Cell {get}
- prop Parent : Object {get}
- prop Row : Int32 {get}
- prop Width : Int32 {get}
- method GetEnumerator() : IEnumerator
- method Merge() : Void
- method Select() : Void

## ColorCMYK
- prop Application : Application {get}
- prop Black : Int32 {get;set}
- prop Cyan : Int32 {get;set}
- prop Magenta : Int32 {get;set}
- prop Parent : Object {get}
- prop Yellow : Int32 {get;set}
- method SetCMYK(Int32 Cyan, Int32 Magenta, Int32 Yellow, Int32 Black) : Void

## ColorFormat
- prop Application : Application {get}
- prop BaseCMYK : ColorCMYK {get}
- prop BaseRGB : Int32 {get;set}
- prop CMYK : ColorCMYK {get}
- prop Ink : Int32 {get;set}
- prop Parent : Object {get}
- prop RGB : Int32 {get;set}
- prop SchemeColor : PbSchemeColorIndex {get;set}
- prop TintAndShade : Single {get;set}
- prop Transparency : Single {get;set}
- prop Type : PbColorType {get}

## ColorScheme
- prop Application : Application {get}
- prop Colors[PbSchemeColorIndex ColorIndex] : ColorFormat {get}
- prop Name : String {get}
- prop Parent : Object {get}

## ColorSchemes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : ColorScheme {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## ColorsInUse
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : ColorFormat {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## Column
- prop Application : Application {get}
- prop Cells : CellRange {get}
- prop Parent : Object {get}
- prop Width : Object {get;set}
- method Delete() : Void

## Columns
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Column {get}
- prop Parent : Object {get}
- method Add(Int32 BeforeColumn) : Column
- method GetEnumerator() : IEnumerator

## ConnectorFormat
- prop Application : Application {get}
- prop BeginConnected : MsoTriState {get}
- prop BeginConnectedShape : Shape {get}
- prop BeginConnectionSite : Int32 {get}
- prop EndConnected : MsoTriState {get}
- prop EndConnectedShape : Shape {get}
- prop EndConnectionSite : Int32 {get}
- prop Parent : Object {get}
- prop Type : MsoConnectorType {get;set}
- method BeginConnect(Shape ConnectedShape, Int32 ConnectionSite) : Void
- method BeginDisconnect() : Void
- method EndConnect(Shape ConnectedShape, Int32 ConnectionSite) : Void
- method EndDisconnect() : Void

## Document

## Documents
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object varDocument] : Document {get}
- prop Parent : Object {get}
- method Add(PbWizard PbWizard, Int32 desid) : Document
- method GetEnumerator() : IEnumerator

## DropCap
- prop Application : Application {get}
- prop FontBold : MsoTriState {get;set}
- prop FontColor : ColorFormat {get}
- prop FontItalic : MsoTriState {get;set}
- prop FontName : String {get;set}
- prop LinesUp : Int32 {get;set}
- prop Parent : Object {get}
- prop Size : Int32 {get;set}
- prop Span : Int32 {get;set}
- method ApplyCustomDropCap(Int32 LinesUp, Int32 Size, Int32 Span, String FontName, Boolean Bold, Boolean Italic) : Void
- method Clear() : Void

## EmailMergeEnvelope
- prop Application : Application {get}
- prop Attachemts : Attachments {get}
- prop Bcc : String {get;set}
- prop Cc : MailMergeDataField {get;set}
- prop Parent : Object {get}
- prop Priority : pbEmailMergePriority {get;set}
- prop Subject : String {get;set}
- prop To : MailMergeDataField {get;set}

## Field
- prop Application : Application {get}
- prop Code : String {get}
- prop Next : Field {get}
- prop Parent : Object {get}
- prop PhoneticGuide : PhoneticGuide {get}
- prop Result : String {get}
- prop TextRange : TextRange {get}
- prop Type : PbFieldType {get}
- method Unlink() : Void

## Fields
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Field {get}
- prop Parent : Object {get}
- method AddHorizontalInVertical(TextRange Range, String Text) : Field
- method AddPhoneticGuide(TextRange Range, String Text, PbPhoneticGuideAlignmentType Alignment, Object Raise, String FontName, Object FontSize) : Field
- method Unlink() : Void

## FillFormat
- prop Application : Application {get}
- prop BackColor : ColorFormat {get;set}
- prop ForeColor : ColorFormat {get;set}
- prop GradientAngle : Single {get;set}
- prop GradientColorType : MsoGradientColorType {get}
- prop GradientDegree : Single {get}
- prop GradientStyle : MsoGradientStyle {get}
- prop GradientVariant : Int32 {get}
- prop Parent : Object {get}
- prop Pattern : MsoPatternType {get}
- prop PresetGradientType : MsoPresetGradientType {get}
- prop PresetTexture : MsoPresetTexture {get}
- prop RotateWithObject : MsoTriState {get;set}
- prop TextureAlignment : MsoTextureAlignment {get;set}
- prop TextureHorizontalScale : Single {get;set}
- prop TextureName : String {get}
- prop TextureOffsetX : Single {get;set}
- prop TextureOffsetY : Single {get;set}
- prop TextureType : MsoTextureType {get}
- prop TextureVerticalScale : Single {get;set}
- prop Transparency : Single {get;set}
- prop Type : MsoFillType {get}
- prop Visible : MsoTriState {get;set}
- method OneColorGradient(MsoGradientStyle Style, Int32 Variant, Single Degree) : Void
- method Patterned(MsoPatternType Pattern) : Void
- method PresetGradient(MsoGradientStyle Style, Int32 Variant, MsoPresetGradientType PresetGradientType) : Void
- method PresetTextured(MsoPresetTexture PresetTexture) : Void
- method Solid() : Void
- method TwoColorGradient(MsoGradientStyle Style, Int32 Variant) : Void
- method UserPicture(String PictureFile) : Void
- method UserTextured(String TextureFile) : Void

## FindReplace
- prop Application : Application {get}
- prop FindText : String {get;set}
- prop Forward : Boolean {get;set}
- prop FoundTextRange : TextRange {get}
- prop MatchAlefHamza : Boolean {get;set}
- prop MatchCase : Boolean {get;set}
- prop MatchDiacritics : Boolean {get;set}
- prop MatchKashida : Boolean {get;set}
- prop MatchWholeWord : Boolean {get;set}
- prop MatchWidth : Boolean {get;set}
- prop Parent : Object {get}
- prop ReplaceScope : PbReplaceScope {get;set}
- prop ReplaceWithText : String {get;set}
- method Clear() : Void
- method Execute() : Boolean

## Font
- prop AllCaps : MsoTriState {get;set}
- prop Application : Application {get}
- prop AttachedToText : Boolean {get}
- prop AutomaticPairKerningThreshold : Object {get;set}
- prop Bold : MsoTriState {get;set}
- prop BoldBi : MsoTriState {get;set}
- prop Color : ColorFormat {get}
- prop ContextualAlternates : MsoTriState {get;set}
- prop DiacriticColor : ColorFormat {get}
- prop Emboss : MsoTriState {get;set}
- prop Engrave : MsoTriState {get;set}
- prop ExpandUsingKashida : MsoTriState {get;set}
- prop Fill : FillFormat {get}
- prop Glow : GlowFormat {get}
- prop Italic : MsoTriState {get;set}
- prop ItalicBi : MsoTriState {get;set}
- prop Kerning : Object {get;set}
- prop Ligature : PbLigaturePresetType {get;set}
- prop Line : LineFormat {get}
- prop Name : String {get;set}
- prop NumberStyle : PbNumberStylesType {get;set}
- prop Outline : MsoTriState {get;set}
- prop Parent : Object {get}
- prop Position : Object {get;set}
- prop Reflection : ReflectionFormat {get}
- prop Scaling : Object {get;set}
- prop Shadow : MsoTriState {get;set}
- prop Size : Object {get;set}
- prop SizeBi : Object {get;set}
- prop SmallCaps : MsoTriState {get;set}
- prop StrikeThrough : MsoTriState {get;set}
- prop StylisticAlternates : Object {get;set}
- prop StylisticSets : Object {get;set}
- prop SubScript : MsoTriState {get;set}
- prop SuperScript : MsoTriState {get;set}
- prop Swash : MsoTriState {get;set}
- prop TextShadow : ShadowFormat {get}
- prop ThreeD : ThreeDFormat {get}
- prop Tracking : Object {get;set}
- prop TrackingPreset : PbTrackingPresetType {get;set}
- prop Underline : PbUnderlineType {get;set}
- prop UseDiacriticColor : MsoTriState {get;set}
- method Duplicate() : Font
- method GetScriptName(PbFontScriptType Script) : String
- method Grow() : Void
- method Reset() : Void
- method SetScriptName(PbFontScriptType Script, String FontName) : Void
- method Shrink() : Void

## FreeformBuilder
- prop Application : Application {get}
- prop Parent : Object {get}
- method AddNodes(MsoSegmentType SegmentType, MsoEditingType EditingType, Object X1, Object Y1, Object X2, Object Y2, Object X3, Object Y3) : Void
- method ConvertToShape() : Shape

## GlowFormat
- prop Color : ColorFormat {get}
- prop Radius : Single {get;set}
- prop Transparency : Single {get;set}
- prop Visible : MsoTriState {get;set}

## GroupShapes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : Shape {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## HeaderFooter
- prop Application : Application {get}
- prop IsHeader : Boolean {get}
- prop Parent : Object {get}
- prop TextRange : TextRange {get}
- method Delete() : Void

## Hyperlink
- prop Address : String {get;set}
- prop Application : Application {get}
- prop EmailSubject : String {get;set}
- prop PageID : Int32 {get;set}
- prop Parent : Object {get}
- prop Range : TextRange {get}
- prop Shape : Shape {get}
- prop TargetType : PbHlinkTargetType {get}
- prop TextToDisplay : String {get;set}
- prop Type : MsoHyperlinkType {get}
- method Delete() : Void
- method Move(Int32 NewIndex) : Void
- method SetPageRelative(PbHlinkTargetType RelativePage) : Void

## Hyperlinks
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Hyperlink {get}
- prop Parent : Object {get}
- method Add(TextRange Text, String Address, PbHlinkTargetType RelativePage, Int32 PageID, String TextToDisplay) : Hyperlink
- method GetEnumerator() : IEnumerator

## ICagNotifySink
- method InsertClip(Object pClipMoniker, Object pItemMoniker) : Int32
- method WindowIsClosing() : Int32

## InlineShapes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object var] : Shape {get}
- prop Parent : Object {get}
- prop Range[Object Index] : ShapeRange {get}
- method GetEnumerator() : IEnumerator

## InstalledPrinters
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : Printer {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## Label
- prop Application : Application {get}
- prop Height : Object {get}
- prop HorizontalGap : Object {get;set}
- prop LeftMargin : Object {get;set}
- prop Name : String {get}
- prop Parent : Object {get}
- prop TopMargin : Object {get;set}
- prop VerticalGap : Object {get;set}
- prop Width : Object {get}

## Labels
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : Label {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## LayoutGuides
- prop Application : Application {get}
- prop ColumnGutterWidth : Single {get;set}
- prop Columns : Int32 {get;set}
- prop GutterCenterlines : Boolean {get;set}
- prop HorizontalBaseLineOffset : Single {get;set}
- prop HorizontalBaseLineSpacing : Single {get;set}
- prop MarginBottom : Object {get;set}
- prop MarginLeft : Object {get;set}
- prop MarginRight : Object {get;set}
- prop MarginTop : Object {get;set}
- prop MirrorGuides : Boolean {get;set}
- prop Parent : Object {get}
- prop RowGutterWidth : Single {get;set}
- prop Rows : Int32 {get;set}
- prop VerticalBaseLineOffset : Single {get;set}
- prop VerticalBaseLineSpacing : Single {get;set}

## LineFormat
- prop Application : Application {get}
- prop BackColor : ColorFormat {get;set}
- prop BeginArrowheadLength : MsoArrowheadLength {get;set}
- prop BeginArrowheadStyle : MsoArrowheadStyle {get;set}
- prop BeginArrowheadWidth : MsoArrowheadWidth {get;set}
- prop CapStyle : MsoLineCapStyle {get;set}
- prop DashStyle : MsoLineDashStyle {get;set}
- prop EndArrowheadLength : MsoArrowheadLength {get;set}
- prop EndArrowheadStyle : MsoArrowheadStyle {get;set}
- prop EndArrowheadWidth : MsoArrowheadWidth {get;set}
- prop ForeColor : ColorFormat {get;set}
- prop GradientAngle : Single {get;set}
- prop GradientColorType : MsoGradientColorType {get}
- prop GradientStyle : MsoGradientStyle {get}
- prop GradientVariant : Int32 {get}
- prop InsetPen : MsoTriState {get;set}
- prop JoinStyle : MsoLineJoinStyle {get;set}
- prop Parent : Object {get}
- prop Pattern : MsoPatternType {get;set}
- prop PresetGradientType : MsoPresetGradientType {get}
- prop Style : MsoLineStyle {get;set}
- prop Transparency : Single {get;set}
- prop Type : MsoLineFillType {get}
- prop Visible : MsoTriState {get;set}
- prop Weight : Object {get;set}
- method PresetGradient(MsoGradientStyle Style, Int32 Variant, MsoPresetGradientType PresetGradientType) : Void

## LinkFormat
- prop Application : Application {get}
- prop Parent : Object {get}
- prop SourceFullName : String {get}
- method Update() : Void

## MailMerge
- prop Application : Application {get}
- prop DataSource : MailMergeDataSource {get}
- prop Destination : Int32 {get}
- prop DocumentUpdating : Boolean {get;set}
- prop EmailMergeEnvelope : EmailMergeEnvelope {get}
- prop Parent : Object {get}
- prop ShowSendToCustom : String {get;set}
- prop SuppressBlankLines : Boolean {get;set}
- prop Type : PbMergeType {get;set}
- prop ViewMailMergeFieldCodes : Boolean {get;set}
- prop WizardState : Int32 {get;set}
- method CreateShortcut(String Filename) : Void
- method Execute(Boolean Pause, PbMailMergeDestination Destination, String Filename) : Document
- method Execute10(Boolean Pause) : Void
- method ExportRecipientList(String Filename, PbRecipientListFileType FileType, Boolean IncludedOnly) : Void
- method OpenDataSource(String bstrDataSource, String bstrConnect, String bstrTable, Int32 fOpenExclusive, Int32 fNeverPrompt) : Void
- method ShowWizard(Boolean ShowDocumentStep, Boolean ShowTemplateStep, Boolean ShowDataStep, Boolean ShowWriteStep, Boolean ShowPreviewStep, Boolean ShowMergeStep) : Void
- method ShowWizardEx(Boolean ShowDocumentStep, Boolean ShowTemplateStep, Boolean ShowDataStep, Boolean ShowWriteStep, Boolean ShowPreviewStep, Boolean ShowMergeStep, PbMergeType MergeType, Int32 iStep) : Void

## MailMergeDataField
- prop Application : Object {get}
- prop Creator : Int32 {get}
- prop FieldType : PbMailMergeDataFieldType {get;set}
- prop Index : Int32 {get}
- prop IsMapped : Boolean {get}
- prop MappedTo : String {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop Value : String {get}
- method AddToRecipientFields() : Void
- method Insert(TextRange Range) : Shape
- method MapToRecipientField(String bstrValue) : Void
- method UnMapRecipientField() : Void

## MailMergeDataFields
- prop Application : Object {get}
- prop Count : Int32 {get}
- prop Creator : Int32 {get}
- prop Item[Object varIndex] : MailMergeDataField {get}
- prop Parent : Object {get}

## MailMergeDataSource
- prop ActiveRecord : Int32 {get;set}
- prop Application : Application {get}
- prop ConnectString : String {get}
- prop DataFields : MailMergeDataFields {get}
- prop DataSources : MailMergeDataSources {get}
- prop EverValidated : Boolean {get;set}
- prop Filters : MailMergeFilters {get}
- prop FirstRecord : Int32 {get;set}
- prop Included : Boolean {get;set}
- prop InvalidAddress : Boolean {get;set}
- prop InvalidComments : String {get;set}
- prop IsMaster : Boolean {get}
- prop LastRecord : Int32 {get;set}
- prop MappedDataFields : MailMergeMappedDataFields {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop RecordCount : Int32 {get}
- prop TableName : String {get}
- prop Type : Int32 {get}
- prop ValidatedClean : Boolean {get;set}
- method ApplyFilter() : Void
- method Close() : Void
- method EditRecord(Int32 lRec, Object varField, Object Value) : Void
- method FindRecord(String FindText, String Field) : Boolean
- method OpenRecipientsDialog() : Void
- method SetAllErrorFlags(Boolean Invalid, String InvalidComment) : Void
- method SetAllIncludedFlags(Boolean Included) : Void
- method SetSortOrder(String SortField1, Boolean SortAscending1, String SortField2, Boolean SortAscending2, String SortField3, Boolean SortAscending3) : Void

## MailMergeDataSources
- prop Application : Object {get}
- prop Count : Int32 {get}
- prop Creator : Int32 {get}
- prop Parent : Object {get}
- method Item(Object varIndex) : MailMergeDataSource

## MailMergeFilterCriterion
- prop Application : Object {get}
- prop Column : String {get;set}
- prop CompareTo : String {get;set}
- prop Comparison : MsoFilterComparison {get;set}
- prop Conjunction : MsoFilterConjunction {get;set}
- prop Creator : Int32 {get}
- prop Index : Int32 {get}
- prop Parent : Object {get}

## MailMergeFilters
- prop Application : Object {get}
- prop Count : Int32 {get}
- prop Creator : Int32 {get}
- prop Item[Int32 Index] : Object {get}
- prop Parent : Object {get}
- method Add(String Column, MsoFilterComparison Comparison, MsoFilterConjunction Conjunction, String bstrCompareTo, Boolean DeferUpdate) : Void
- method Delete(Int32 Index, Boolean DeferUpdate) : Void

## MailMergeMappedDataField
- prop Application : Application {get}
- prop DataFieldName : String {get}
- prop Index : Int32 {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop Value : String {get}

## MailMergeMappedDataFields
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : MailMergeMappedDataField {get}
- prop Parent : Object {get}

## MasterPages
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : Page {get}
- prop Parent : Object {get}
- method Add(Boolean IsTwoPageMaster, String Abbreviation, String Description) : Page
- method FindByPageID(Int32 PageID) : Page
- method GetEnumerator() : IEnumerator

## ModalBrowser
- method MoveTo(Int32 lx, Int32 ly) : Void
- method ResizeTo(Int32 lWidth, Int32 lHeight) : Void
- method TaskCompleted() : Void

## ObjectVerbs
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : String {get}
- prop Parent : Object {get}

## OLEFormat
- prop Application : Application {get}
- prop Object : Object {get}
- prop ObjectVerbs : ObjectVerbs {get}
- prop Parent : Object {get}
- prop ProgId : String {get}
- method Activate() : Void
- method DoVerb(Int32 iVerb) : Void

## Options
- prop AddHebDoubleQuote : Boolean {get;set}
- prop AllowBackgroundSave : Boolean {get;set}
- prop Application : Application {get}
- prop AutoFormatWord : Boolean {get;set}
- prop AutoHyphenate : Boolean {get;set}
- prop AutoKeyboardSwitching : Boolean {get;set}
- prop AutoSelectWord : Boolean {get;set}
- prop DefaultPubDirection : PbDirectionType {get;set}
- prop DefaultTextFlowDirection : PbDirectionType {get;set}
- prop DisplayPrintTroubleshooter : Boolean {get;set}
- prop DisplayStatusBar : Boolean {get;set}
- prop DragAndDropText : Boolean {get;set}
- prop EnvelopePrintOrientation : PbOrientationType {get;set}
- prop EnvelopePrintPlacement : PbPlacementType {get;set}
- prop HyphenationZone : Object {get;set}
- prop MeasurementUnit : PbUnitType {get;set}
- prop Parent : Object {get}
- prop PathForPictures : String {get;set}
- prop PathForPublications : String {get;set}
- prop PrintLineByLine : Boolean {get;set}
- prop SaveAutoRecoverInfo : Boolean {get;set}
- prop SaveAutoRecoverInfoInterval : Int32 {get;set}
- prop SequenceCheck : Boolean {get;set}
- prop ShowBasicColors : Boolean {get;set}
- prop ShowScreenTipsOnObjects : Boolean {get;set}
- prop ShowTipPages : Boolean {get;set}
- prop TypeNReplace : Boolean {get;set}
- prop UpdatePersonalInfoOnSave : Boolean {get;set}
- prop UseCatalogAtStartup : Boolean {get;set}
- prop UseEnvelopePaperSizes : Boolean {get;set}
- prop UseEnvelopePrintOptions : Boolean {get;set}
- prop UseHelpfulMousePointers : Boolean {get;set}
- prop UseWizardForBlankPublication : Boolean {get;set}
- method ResetTips() : Void
- method ResetWizardSynchronizing() : Void

## Page
- prop Application : Application {get}
- prop Background : PageBackground {get}
- prop Footer : HeaderFooter {get}
- prop Header : HeaderFooter {get}
- prop Height : Int32 {get}
- prop IgnoreMaster : Boolean {get;set}
- prop IsLeading : Boolean {get}
- prop IsTrailing : Boolean {get}
- prop IsTwoPageMaster : Boolean {get;set}
- prop IsWizardPage : Boolean {get}
- prop LayoutGuides : LayoutGuides {get}
- prop Master : Page {get;set}
- prop Name : String {get;set}
- prop PageID : Int32 {get}
- prop PageIndex : Int32 {get}
- prop PageNumber : String {get;set}
- prop PageType : PbPageType {get}
- prop Parent : Object {get}
- prop ReaderSpread : ReaderSpread {get}
- prop RulerGuides : RulerGuides {get}
- prop Shapes : Shapes {get}
- prop Tags : Tags {get}
- prop WebPageOptions : WebPageOptions {get}
- prop Width : Int32 {get}
- prop Wizard : Wizard {get}
- prop XOffsetWithinReaderSpread : Single {get}
- prop YOffsetWithinReaderSpread : Single {get}
- method Delete() : Void
- method Duplicate(String NewAbbreviation, String NewName) : Page
- method ExportEmailHTML(String Filename) : Void
- method Move(Int32 Page, Boolean After) : Void
- method SaveAsPicture(String Filename, PbPictureResolution pbResolution) : Void
- method SaveAsPicture10(String Filename) : Void

## PageBackground
- prop Application : Application {get}
- prop Exists : Boolean {get}
- prop Fill : FillFormat {get}
- prop Parent : Object {get}
- method Create() : Void
- method Delete() : Void

## Pages
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : Page {get}
- prop Parent : Object {get}
- method Add(Int32 Count, Int32 After, Int32 DuplicateObjectsOnPage, Boolean AddHyperlinkToWebNavBar) : Page
- method Add10(Int32 Count, Int32 After, Int32 DuplicateObjectsOnPage) : Page
- method AddWizardPage(Int32 After, PbWizardPageType PageType, Boolean AddHyperlinkToWebNavBar) : Void
- method AddWizardPage10(Int32 After, PbWizardPageType PageType) : Void
- method FindByPageID(Int32 PageID) : Page
- method GetEnumerator() : IEnumerator

## PageSetup
- prop Application : Application {get}
- prop AvailableLabels : Labels {get}
- prop AvailablePageSizes : PageSizes {get}
- prop HorizontalGap : Object {get;set}
- prop Label : Label {get;set}
- prop LeftMargin : Object {get;set}
- prop MultiplePagesPerSheet : Boolean {get;set}
- prop Orientation : PbOrientationType {get;set}
- prop PageHeight : Object {get;set}
- prop PageSize : PageSize {get;set}
- prop PageWidth : Object {get;set}
- prop Parent : Object {get}
- prop PublicationLayout : PbPublicationLayout {get;set}
- prop TopMargin : Object {get;set}
- prop VerticalGap : Object {get;set}

## PageSize
- prop Application : Application {get}
- prop HasBackgroundImage : Boolean {get}
- prop HorizontalGap : Object {get}
- prop LeftMargin : Object {get}
- prop Name : String {get}
- prop PageHeight : Object {get;set}
- prop PageWidth : Object {get;set}
- prop Parent : Object {get}
- prop TopMargin : Object {get}
- prop VerticalGap : Object {get}

## PageSizes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : PageSize {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## ParagraphFormat
- prop Alignment : PbParagraphAlignmentType {get;set}
- prop Application : Application {get}
- prop AttachedToText : Boolean {get}
- prop CharBasedFirstLineIndent : Int32 {get;set}
- prop FirstLineIndent : Object {get;set}
- prop KashidaPercentage : Int32 {get;set}
- prop KeepLinesTogether : MsoTriState {get;set}
- prop KeepWithNext : MsoTriState {get;set}
- prop LeftIndent : Object {get;set}
- prop LineSpacing : Object {get;set}
- prop LineSpacingRule : PbLineSpacingRule {get;set}
- prop ListBulletFontName : String {get;set}
- prop ListBulletFontSize : Single {get;set}
- prop ListBulletText : String {get}
- prop ListIndent : Single {get;set}
- prop ListNumberSeparator : PbListSeparator {get;set}
- prop ListNumberStart : Int32 {get;set}
- prop ListType : PbListType {get}
- prop LockToBaseLine : MsoTriState {get;set}
- prop Parent : Object {get}
- prop RightIndent : Object {get;set}
- prop SpaceAfter : Object {get;set}
- prop SpaceBefore : Object {get;set}
- prop StartInNextTextBox : MsoTriState {get;set}
- prop Tabs : TabStops {get}
- prop TextDirection : PbTextDirection {get;set}
- prop TextStyle : Object {get;set}
- prop UseCharBasedFirstLineIndent : MsoTriState {get;set}
- prop WidowControl : MsoTriState {get;set}
- method Duplicate() : ParagraphFormat
- method Reset() : Void
- method SetLineSpacing(PbLineSpacingRule Rule, Object Spacing) : Void
- method SetListType(PbListType Value, String BulletText) : Void

## PhoneticGuide
- prop Alignment : PbPhoneticGuideAlignmentType {get}
- prop Application : Application {get}
- prop BaseText : String {get}
- prop FontName : String {get}
- prop FontSize : Object {get}
- prop Parent : Object {get}
- prop Raise : Object {get}
- prop Text : String {get}
- method Clear() : Void

## PictureFormat
- prop Application : Application {get}
- prop Brightness : Single {get;set}
- prop ColorModel : PbColorModel {get}
- prop ColorsInPalette : Int32 {get}
- prop ColorType : MsoPictureColorType {get;set}
- prop Contrast : Single {get;set}
- prop CropBottom : Object {get;set}
- prop CropLeft : Object {get;set}
- prop CropRight : Object {get;set}
- prop CropTop : Object {get;set}
- prop EffectiveResolution : Int32 {get}
- prop Filename : String {get}
- prop FileSize : Int32 {get}
- prop HasAlphaChannel : MsoTriState {get}
- prop HasTransparencyColor : Boolean {get}
- prop Height : Object {get}
- prop HorizontalPictureLocking : PbHorizontalPictureLocking {get;set}
- prop HorizontalScale : Int32 {get}
- prop ImageFormat : PbImageFormat {get}
- prop IsEmpty : MsoTriState {get}
- prop IsGreyScale : MsoTriState {get}
- prop IsLinked : MsoTriState {get}
- prop IsRecolored : MsoTriState {get}
- prop IsTrueColor : MsoTriState {get}
- prop LeaveBlackAsBlack : MsoTriState {get}
- prop LinkedFileStatus : PbLinkedFileStatus {get}
- prop OriginalColorsInPalette : Int32 {get}
- prop OriginalFileSize : Int32 {get}
- prop OriginalHasAlphaChannel : MsoTriState {get}
- prop OriginalHeight : Object {get}
- prop OriginalIsTrueColor : MsoTriState {get}
- prop OriginalResolution : Int32 {get}
- prop OriginalWidth : Object {get}
- prop Parent : Object {get}
- prop RecoloredPictureColor : ColorFormat {get}
- prop TransparencyColor : Int32 {get;set}
- prop TransparentBackground : MsoTriState {get;set}
- prop VerticalPictureLocking : PbVerticalPictureLocking {get;set}
- prop VerticalScale : Int32 {get}
- prop Width : Object {get}
- method ClearCrop() : Void
- method FillFrame() : Void
- method FitFrame() : Void
- method IncrementBrightness(Single Increment) : Void
- method IncrementContrast(Single Increment) : Void
- method Recolor(ColorFormat Color, MsoTriState LeaveBlackPartsBlack) : Void
- method Remove() : Void
- method Replace(String Pathname, PbPictureInsertAs InsertAs) : Void
- method ReplaceEx(String Pathname, PbPictureInsertAs InsertAs, pbPictureInsertFit Fit) : Void
- method RestoreOriginalColors() : Void

## Plate
- prop Angle : Int32 {get;set}
- prop Application : Application {get}
- prop Color : ColorFormat {get}
- prop Frequency : Int32 {get;set}
- prop Index : Int32 {get}
- prop InkName : PbInkName {get}
- prop InUse : Boolean {get}
- prop Luminance : Int32 {get;set}
- prop Name : String {get}
- prop Parent : Object {get}
- method ConvertToProcess() : Void
- method Delete(Object PlateReplaceWith, PbReplaceTint ReplaceTint) : Void
- method Delete10() : Void

## Plates
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Plate {get}
- prop Parent : Object {get}
- method Add(ColorFormat PlateColor) : Void
- method FindPlateByInkName(PbInkName InkName) : Plate
- method GetEnumerator() : IEnumerator

## PrintablePlate
- prop Angle : Int32 {get;set}
- prop Application : Application {get}
- prop Frequency : Int32 {get;set}
- prop Index : Int32 {get}
- prop InkName : PbInkName {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop PrintPlate : Boolean {get;set}

## PrintablePlates
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : PrintablePlate {get}
- prop Parent : Object {get}
- method FindPlateByInkName(PbInkName InkName) : PrintablePlate
- method GetEnumerator() : IEnumerator

## PrintableRect
- prop Application : Application {get}
- prop Height : Single {get}
- prop Left : Single {get}
- prop Parent : Object {get}
- prop Top : Single {get}
- prop Width : Single {get}

## Printer
- prop Application : Application {get}
- prop DriverType : PbDriverType {get}
- prop Index : Int32 {get}
- prop IsActivePrinter : Boolean {get;set}
- prop IsColor : Boolean {get}
- prop IsDuplex : Boolean {get}
- prop PaperHeight : Int32 {get;set}
- prop PaperOrientation : PbOrientationType {get;set}
- prop PaperSize : String {get}
- prop PaperSource : String {get}
- prop PaperWidth : Int32 {get;set}
- prop Parent : Object {get}
- prop PrintableRect : PrintableRect {get}
- prop PrinterName : String {get}
- prop PrintMode : PbPrintMode {get;set}

## ReaderSpread
- prop Application : Application {get}
- prop Height : Single {get}
- prop Left : Single {get}
- prop PageCount : Int32 {get}
- prop Pages[Int32 Index] : Page {get}
- prop Parent : Object {get}
- prop Top : Single {get}
- prop Width : Single {get}

## ReflectionFormat
- prop Blur : Single {get;set}
- prop Offset : Single {get;set}
- prop Size : Single {get;set}
- prop Transparency : Single {get;set}
- prop Type : MsoReflectionType {get;set}
- prop Visible : MsoTriState {get;set}

## Row
- prop Application : Application {get}
- prop Cells : CellRange {get}
- prop Height : Object {get;set}
- prop Parent : Object {get}
- method Delete() : Void

## Rows
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Row {get}
- prop Parent : Object {get}
- method Add(Int32 BeforeRow) : Row
- method GetEnumerator() : IEnumerator

## RulerGuide
- prop Application : Application {get}
- prop Parent : Object {get}
- prop Position : Object {get;set}
- prop Type : PbRulerGuideType {get;set}
- method Delete() : Void

## RulerGuides
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : RulerGuide {get}
- prop Parent : Object {get}
- method Add(Object Position, PbRulerGuideType Type) : Void
- method GetEnumerator() : IEnumerator

## ScratchArea
- prop Application : Application {get}
- prop Parent : Object {get}
- prop Shapes : Shapes {get}

## Section
- prop Application : Application {get}
- prop ContinueNumbersFromPreviousSection : Boolean {get;set}
- prop PageNumberFormat : PbPageNumberFormat {get;set}
- prop PageNumberStart : Int32 {get;set}
- prop Parent : Object {get}
- prop ShowHeaderFooterOnFirstPage : Boolean {get;set}
- prop StartPageIndex : Int32 {get}
- method Delete() : Void

## Sections
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Section {get}
- prop Parent : Object {get}
- method Add(Int32 StartPageIndex) : Section
- method GetEnumerator() : IEnumerator

## Selection
- prop Application : Application {get}
- prop ChildShapeRange : ShapeRange {get}
- prop Parent : Object {get}
- prop ShapeRange : ShapeRange {get}
- prop TableCellRange : CellRange {get}
- prop TextRange : TextRange {get}
- prop Type : PbSelectionType {get}
- method Unselect() : Void

## ShadowFormat
- prop Application : Application {get}
- prop Blur : Single {get;set}
- prop ForeColor : ColorFormat {get;set}
- prop Obscured : MsoTriState {get;set}
- prop OffsetX : Object {get;set}
- prop OffsetY : Object {get;set}
- prop Parent : Object {get}
- prop RotateWithShape : MsoTriState {get;set}
- prop Size : Single {get;set}
- prop Transparency : Single {get;set}
- prop Type : MsoShadowType {get;set}
- prop Visible : MsoTriState {get;set}
- method IncrementOffsetX(Object Increment) : Void
- method IncrementOffsetY(Object Increment) : Void

## Shape
- prop Adjustments : Adjustments {get}
- prop AlternativeText : String {get;set}
- prop Application : Application {get}
- prop AutoShapeType : MsoAutoShapeType {get;set}
- prop BlackWhiteMode : MsoBlackWhiteMode {get;set}
- prop BorderArt : BorderArtFormat {get}
- prop Callout : CalloutFormat {get}
- prop CatalogMergeItems : CatalogMergeShapes {get}
- prop ConnectionSiteCount : Int32 {get}
- prop Connector : MsoTriState {get}
- prop ConnectorFormat : ConnectorFormat {get}
- prop Fill : FillFormat {get}
- prop Glow : GlowFormat {get}
- prop GroupItems : GroupShapes {get}
- prop HasTable : MsoTriState {get}
- prop HasTextFrame : MsoTriState {get}
- prop Height : Object {get;set}
- prop HorizontalFlip : MsoTriState {get}
- prop Hyperlink : Hyperlink {get}
- prop ID : Int32 {get}
- prop InlineAlignment : PbInlineAlignment {get;set}
- prop InlineTextRange : TextRange {get}
- prop IsExcess : MsoTriState {get}
- prop IsGroupMember : Boolean {get}
- prop IsInline : MsoTriState {get}
- prop Left : Object {get;set}
- prop Line : LineFormat {get}
- prop LinkFormat : LinkFormat {get}
- prop LockAspectRatio : MsoTriState {get;set}
- prop Name : String {get;set}
- prop Nodes : ShapeNodes {get}
- prop OLEFormat : OLEFormat {get}
- prop Parent : Object {get}
- prop ParentGroupShape : Shape {get}
- prop PictureFormat : PictureFormat {get}
- prop Reflection : ReflectionFormat {get}
- prop Rotation : Single {get;set}
- prop Shadow : ShadowFormat {get}
- prop SoftEdge : SoftEdgeFormat {get}
- prop Table : Table {get}
- prop Tags : Tags {get}
- prop TextEffect : TextEffectFormat {get}
- prop TextFrame : TextFrame {get}
- prop TextWrap : WrapFormat {get}
- prop ThreeD : ThreeDFormat {get}
- prop Top : Object {get;set}
- prop Type : PbShapeType {get}
- prop VerticalFlip : MsoTriState {get}
- prop Vertices : Object {get}
- prop WebCheckBox : WebCheckBox {get}
- prop WebCommandButton : WebCommandButton {get}
- prop WebComponentFormat : WebComponentFormat {get}
- prop WebListBox : WebListBox {get}
- prop WebNavigationBarSetName : String {get}
- prop WebOptionButton : WebOptionButton {get}
- prop WebTextBox : WebTextBox {get}
- prop Width : Object {get;set}
- prop Wizard : Wizard {get}
- prop WizardTag : PbWizardTag {get;set}
- prop WizardTagInstance : Int32 {get;set}
- prop ZOrderPosition : Int32 {get}
- method AddToCatalogMergeArea() : Void
- method Apply() : Void
- method Copy() : Void
- method Cut() : Void
- method Delete() : Void
- method Duplicate() : Shape
- method Flip(MsoFlipCmd FlipCmd) : Void
- method GetHeight(PbUnitType Unit) : Single
- method GetLeft(PbUnitType Unit) : Single
- method GetTop(PbUnitType Unit) : Single
- method GetWidth(PbUnitType Unit) : Single
- method IncrementLeft(Object Increment) : Void
- method IncrementRotation(Single Increment) : Void
- method IncrementTop(Object Increment) : Void
- method MoveIntoTextFlow(TextRange Range) : Void
- method MoveOutOfTextFlow() : Void
- method MoveToPage(Int32 Page, Object Left, Object Top) : Void
- method PickUp() : Void
- method RemoveCatalogMergeArea() : Void
- method RemoveFromCatalogMergeArea() : Void
- method RerouteConnections() : Void
- method SaveAsBuildingBlock(String Name) : BuildingBlock
- method SaveAsPicture(String Filename, PbPictureResolution pbResolution) : Void
- method ScaleHeight(Single Factor, MsoTriState RelativeToOriginalSize, MsoScaleFrom fScale) : Void
- method ScaleWidth(Single Factor, MsoTriState RelativeToOriginalSize, MsoScaleFrom fScale) : Void
- method Select(Object Replace) : Void
- method SetCaption(CaptionStyle Style) : Shape
- method SetShapesDefaultProperties() : Void
- method Ungroup() : ShapeRange
- method ZOrder(MsoZOrderCmd ZOrderCmd) : Void

## ShapeNode
- prop Application : Application {get}
- prop EditingType : MsoEditingType {get}
- prop Parent : Object {get}
- prop Points : Object {get}
- prop SegmentType : MsoSegmentType {get}

## ShapeNodes
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : ShapeNode {get}
- prop Parent : Object {get}
- method Delete(Int32 Index) : Void
- method GetEnumerator() : IEnumerator
- method Insert(Int32 Index, MsoSegmentType SegmentType, MsoEditingType EditingType, Object X1, Object Y1, Object X2, Object Y2, Object X3, Object Y3) : Void
- method SetEditingType(Int32 Index, MsoEditingType EditingType) : Void
- method SetPosition(Int32 Index, Object X1, Object Y1) : Void
- method SetSegmentType(Int32 Index, MsoSegmentType SegmentType) : Void

## ShapeRange
- prop Adjustments : Adjustments {get}
- prop AlternativeText : String {get;set}
- prop Application : Application {get}
- prop AutoShapeType : MsoAutoShapeType {get;set}
- prop BlackWhiteMode : MsoBlackWhiteMode {get;set}
- prop Callout : CalloutFormat {get}
- prop ConnectionSiteCount : Int32 {get}
- prop Connector : MsoTriState {get}
- prop ConnectorFormat : ConnectorFormat {get}
- prop Count : Int32 {get}
- prop Fill : FillFormat {get}
- prop Glow : GlowFormat {get}
- prop GroupItems : GroupShapes {get}
- prop HasTable : MsoTriState {get}
- prop HasTextFrame : MsoTriState {get}
- prop Height : Object {get}
- prop HorizontalFlip : MsoTriState {get}
- prop Hyperlink : Hyperlink {get}
- prop ID : Int32 {get}
- prop InlineAlignment : PbInlineAlignment {get;set}
- prop InlineTextRange : TextRange {get}
- prop IsInline : MsoTriState {get}
- prop Item[Object Index] : Shape {get}
- prop Left : Object {get}
- prop Line : LineFormat {get}
- prop LinkFormat : LinkFormat {get}
- prop LockAspectRatio : MsoTriState {get;set}
- prop Name : String {get;set}
- prop Nodes : ShapeNodes {get}
- prop OLEFormat : OLEFormat {get}
- prop Parent : Object {get}
- prop PictureFormat : PictureFormat {get}
- prop Reflection : ReflectionFormat {get}
- prop Rotation : Single {get;set}
- prop Shadow : ShadowFormat {get}
- prop SoftEdge : SoftEdgeFormat {get}
- prop Table : Table {get}
- prop Tags : Tags {get}
- prop TextEffect : TextEffectFormat {get}
- prop TextFrame : TextFrame {get}
- prop TextWrap : WrapFormat {get}
- prop ThreeD : ThreeDFormat {get}
- prop Top : Object {get}
- prop Type : PbShapeType {get}
- prop VerticalFlip : MsoTriState {get}
- prop Vertices : Object {get}
- prop Width : Object {get}
- prop Wizard : Wizard {get}
- prop WizardTag : PbWizardTag {get;set}
- prop WizardTagInstance : Int32 {get;set}
- prop ZOrderPosition : Int32 {get}
- method AddToCatalogMergeArea() : Void
- method Align(MsoAlignCmd AlignCmd, MsoTriState RelativeTo) : Void
- method Apply() : Void
- method Copy() : Void
- method Cut() : Void
- method Delete() : Void
- method Distribute(MsoDistributeCmd DistributeCmd, MsoTriState RelativeTo) : Void
- method Duplicate() : ShapeRange
- method Flip(MsoFlipCmd FlipCmd) : Void
- method GetEnumerator() : IEnumerator
- method GetHeight(PbUnitType Unit) : Single
- method GetLeft(PbUnitType Unit) : Single
- method GetTop(PbUnitType Unit) : Single
- method GetWidth(PbUnitType Unit) : Single
- method Group() : Shape
- method IncrementLeft(Object Increment) : Void
- method IncrementRotation(Single Increment) : Void
- method IncrementTop(Object Increment) : Void
- method MoveIntoTextFlow(TextRange Range) : Void
- method MoveOutOfTextFlow() : Void
- method PickUp() : Void
- method Regroup() : Shape
- method RemoveFromCatalogMergeArea() : Void
- method RerouteConnections() : Void
- method SaveAsBuildingBlock(String Name) : BuildingBlock
- method SaveAsPicture(String Filename, PbPictureResolution pbResolution) : Void
- method ScaleHeight(Single Factor, MsoTriState RelativeToOriginalSize, MsoScaleFrom fScale) : Void
- method ScaleWidth(Single Factor, MsoTriState RelativeToOriginalSize, MsoScaleFrom fScale) : Void
- method Select(Object Replace) : Void
- method SetShapesDefaultProperties() : Void
- method Ungroup() : ShapeRange
- method ZOrder(MsoZOrderCmd ZOrderCmd) : Void

## Shapes
- prop Application : Application {get}
- prop CanvasArrangementType : pbCanvasArrangementType {get;set}
- prop CanvasesCount : Int32 {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : Shape {get}
- prop Parent : Object {get}
- method AddBuildingBlock(BuildingBlock BBlockIn, Object Left, Object Top) : Shape
- method AddCallout(MsoCalloutType Type, Object Left, Object Top, Object Width, Object Height) : Shape
- method AddCatalogMergeArea() : Shape
- method AddCatalogMergeFieldToCanvas(Int32 CanvasId, pbCatalogMergeFieldType CatalogMergeFieldType, Int32 DbCol) : Void
- method AddConnector(MsoConnectorType Type, Object BeginX, Object BeginY, Object EndX, Object EndY) : Shape
- method AddCurve(Object SafeArrayOfPoints) : Shape
- method AddEmptyPictureFrame(Object Left, Object Top, Object Width, Object Height) : Shape
- method AddGroupWizard(PbWizardGroup Wizard, Object Left, Object Top, Object Width, Object Height, Int32 Design) : Shape
- method AddLabel(PbTextOrientation Orientation, Object Left, Object Top, Object Width, Object Height) : Shape
- method AddLine(Object BeginX, Object BeginY, Object EndX, Object EndY) : Shape
- method AddOLEObject(Object Left, Object Top, Object Width, Object Height, String ClassName, String Filename, MsoTriState Link) : Shape
- method AddPicture(String Filename, MsoTriState LinkToFile, MsoTriState SaveWithDocument, Object Left, Object Top, Object Width, Object Height) : Shape
- method AddPolyline(Object SafeArrayOfPoints) : Shape
- method AddShape(MsoAutoShapeType Type, Object Left, Object Top, Object Width, Object Height) : Shape
- method AddTable(Int32 NumRows, Int32 NumColumns, Object Left, Object Top, Object Width, Object Height, Boolean FixedSize, PbTableDirectionType Direction) : Shape
- method AddTextbox(PbTextOrientation Orientation, Object Left, Object Top, Object Width, Object Height) : Shape
- method AddTextEffect(MsoPresetTextEffect PresetTextEffect, String Text, String FontName, Object FontSize, MsoTriState FontBold, MsoTriState FontItalic, Object Left, Object Top) : Shape
- method AddWebControl(PbWebControlType Type, Object Left, Object Top, Object Width, Object Height, Boolean LaunchPropertiesWindow) : Shape
- method AddWebNavigationBar(String Name, Object Left, Object Top, Object Width) : Shape
- method AddWordArt(pbPresetWordArt PresetWordArt, String Text, String FontName, Object FontSize, MsoTriState FontBold, MsoTriState FontItalic, Object Left, Object Top) : Shape
- method BuildFreeform(MsoEditingType EditingType, Object X1, Object Y1) : FreeformBuilder
- method FindShapeByWizardTag(PbWizardTag WizardTag, Int32 Instance) : ShapeRange
- method GetEnumerator() : IEnumerator
- method Paste() : ShapeRange
- method Range(Object Index) : ShapeRange
- method SelectAll() : Void

## SoftEdgeFormat
- prop Radius : Single {get;set}
- prop Type : MsoSoftEdgeType {get;set}
- prop Visible : MsoTriState {get;set}

## Stories
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Story {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## Story
- prop Application : Application {get}
- prop HasTable : MsoTriState {get}
- prop HasTextFrame : MsoTriState {get}
- prop Parent : Object {get}
- prop Table : Table {get}
- prop TextFrame : TextFrame {get}
- prop TextRange : TextRange {get}
- prop Type : PbStoryType {get}

## Table
- prop Application : Application {get}
- prop Cells[Int32 StartRow, Int32 StartColumn, Int32 EndRow, Int32 EndColumn] : CellRange {get}
- prop Columns : Columns {get}
- prop GrowToFitText : Boolean {get;set}
- prop Parent : Object {get}
- prop Rows : Rows {get}
- prop TableDirection : PbTableDirectionType {get;set}
- method ApplyAutoFormat(PbTableAutoFormatType AutoFormat, Boolean TextFormatting, Boolean TextAlignment, Boolean Fill, Boolean Borders) : Void

## TabStop
- prop Alignment : PbTabAlignmentType {get;set}
- prop Application : Application {get}
- prop Leader : PbTabLeaderType {get;set}
- prop Parent : Object {get}
- prop Position : Object {get;set}
- method Clear() : Void

## TabStops
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : TabStop {get}
- prop Parent : Object {get}
- method Add(Object Position, PbTabAlignmentType Alignment, PbTabLeaderType Leader) : Void
- method ClearAll() : Void

## Tag
- prop Application : Application {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop Value : Object {get;set}
- method Delete() : Void

## Tags
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : Tag {get}
- prop Parent : Object {get}
- method Add(String Name, Object Value) : Tag
- method GetEnumerator() : IEnumerator

## TextEffectFormat
- prop Alignment : MsoTextEffectAlignment {get;set}
- prop Application : Application {get}
- prop FontBold : MsoTriState {get;set}
- prop FontItalic : MsoTriState {get;set}
- prop FontName : String {get;set}
- prop FontSize : Object {get;set}
- prop KernedPairs : MsoTriState {get;set}
- prop NormalizedHeight : MsoTriState {get;set}
- prop Parent : Object {get}
- prop PresetShape : MsoPresetTextEffectShape {get;set}
- prop PresetTextEffect : MsoPresetTextEffect {get;set}
- prop PresetWordArt : pbPresetWordArt {get;set}
- prop RotatedChars : MsoTriState {get;set}
- prop Text : String {get;set}
- prop Tracking : Object {get;set}
- method ToggleVerticalText() : Void

## TextFrame
- prop Application : Application {get}
- prop AutoFitText : PbTextAutoFitType {get;set}
- prop Columns : Int32 {get;set}
- prop ColumnSpacing : Object {get;set}
- prop HasNextLink : MsoTriState {get}
- prop HasPreviousLink : MsoTriState {get}
- prop HasText : MsoTriState {get}
- prop IncludeContinuedFromPage : MsoTriState {get;set}
- prop IncludeContinuedOnPage : MsoTriState {get;set}
- prop MarginBottom : Object {get;set}
- prop MarginLeft : Object {get;set}
- prop MarginRight : Object {get;set}
- prop MarginTop : Object {get;set}
- prop NextLinkedTextFrame : TextFrame {get;set}
- prop Orientation : PbTextOrientation {get;set}
- prop Overflowing : MsoTriState {get}
- prop Parent : Object {get}
- prop PreviousLinkedTextFrame : TextFrame {get}
- prop Story : Story {get}
- prop TextRange : TextRange {get}
- prop VerticalTextAlignment : PbVerticalTextAlignmentType {get;set}
- method BreakForwardLink() : Void
- method ValidLinkTarget(Shape LinkTarget) : Boolean

## TextRange
- prop Application : Application {get}
- prop BoundHeight : Single {get}
- prop BoundLeft : Single {get}
- prop BoundTop : Single {get}
- prop BoundWidth : Single {get}
- prop ContainingObject : Object {get}
- prop DropCap : DropCap {get}
- prop Duplicate : TextRange {get}
- prop End : Int32 {get;set}
- prop Fields : Fields {get}
- prop Find : FindReplace {get}
- prop Font : Font {get;set}
- prop Hyperlinks : Hyperlinks {get}
- prop InlineShapes : InlineShapes {get}
- prop LanguageID : MsoLanguageID {get;set}
- prop Length : Int32 {get}
- prop LinesCount : Int32 {get}
- prop MajorityFont : Font {get}
- prop MajorityParagraphFormat : ParagraphFormat {get}
- prop ParagraphFormat : ParagraphFormat {get;set}
- prop ParagraphsCount : Int32 {get}
- prop Parent : Object {get}
- prop Script : PbFontScriptType {get}
- prop Start : Int32 {get;set}
- prop Story : Story {get}
- prop Text : String {get;set}
- prop WordsCount : Int32 {get}
- method Characters(Int32 Start, Int32 Length) : TextRange
- method Collapse(PbCollapseDirection Direction) : Void
- method Copy() : Void
- method Cut() : Void
- method Delete() : Void
- method Expand(PbTextUnit Unit) : Int32
- method InsertAfter(String NewText) : TextRange
- method InsertBarcode() : TextRange
- method InsertBefore(String NewText) : TextRange
- method InsertDateTime(PbDateTimeFormat Format, Boolean InsertAsField, Boolean InsertAsFullWidth, MsoLanguageID Language, PbCalendarType Calendar) : TextRange
- method InsertMailMergeField(Object varIndex) : TextRange
- method InsertPageNumber(PbPageNumberType Type) : TextRange
- method InsertSymbol(String FontName, Int32 CharIndex) : TextRange
- method Lines(Int32 Start, Int32 Length) : TextRange
- method Move(PbTextUnit Unit, Int32 Size) : Int32
- method MoveEnd(PbTextUnit Unit, Int32 Size) : Int32
- method MoveStart(PbTextUnit Unit, Int32 Size) : Int32
- method Paragraphs(Int32 Start, Int32 Length) : TextRange
- method Paste() : TextRange
- method Select() : Void
- method Words(Int32 Start, Int32 Length) : TextRange

## TextStyle
- prop Application : Application {get}
- prop BaseStyle : String {get;set}
- prop Description : String {get}
- prop Font : Font {get;set}
- prop Name : String {get}
- prop NextParagraphStyle : String {get;set}
- prop ParagraphFormat : ParagraphFormat {get;set}
- prop Parent : Object {get}
- method Delete() : Void

## TextStyles
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : TextStyle {get}
- prop Parent : Object {get}
- method Add(String StyleName, String BasedOn, Font Font, ParagraphFormat ParagraphFormat) : TextStyle
- method GetEnumerator() : IEnumerator

## ThreeDFormat
- prop Application : Application {get}
- prop BevelBottomDepth : Single {get;set}
- prop BevelBottomInset : Single {get;set}
- prop BevelBottomType : MsoBevelType {get;set}
- prop BevelTopDepth : Single {get;set}
- prop BevelTopInset : Single {get;set}
- prop BevelTopType : MsoBevelType {get;set}
- prop ContourColor : ColorFormat {get}
- prop ContourWidth : Single {get;set}
- prop Depth : Object {get;set}
- prop ExtrusionColor : ColorFormat {get}
- prop ExtrusionColorType : MsoExtrusionColorType {get;set}
- prop FieldOfView : Single {get;set}
- prop Parent : Object {get}
- prop Perspective : MsoTriState {get;set}
- prop PresetExtrusionDirection : MsoPresetExtrusionDirection {get}
- prop PresetLightingDirection : MsoPresetLightingDirection {get;set}
- prop PresetLightingSoftness : MsoPresetLightingSoftness {get;set}
- prop PresetMaterial : MsoPresetMaterial {get;set}
- prop PresetThreeDFormat : MsoPresetThreeDFormat {get}
- prop RotationX : Single {get;set}
- prop RotationY : Single {get;set}
- prop Visible : MsoTriState {get;set}
- method IncrementRotationX(Single Increment) : Void
- method IncrementRotationY(Single Increment) : Void
- method ResetRotation() : Void
- method SetExtrusionDirection(MsoPresetExtrusionDirection PresetExtrusionDirection) : Void
- method SetThreeDFormat(MsoPresetThreeDFormat PresetThreeDFormat) : Void

## View
- prop ActivePage : Page {get;set}
- prop Application : Application {get}
- prop Parent : Object {get}
- prop Zoom : PbZoom {get;set}
- method ScrollShapeIntoView(Shape Shape) : Void
- method ZoomIn() : Void
- method ZoomOut() : Void

## WebCheckBox
- prop Application : Application {get}
- prop Parent : Object {get}
- prop ReturnDataLabel : String {get;set}
- prop Selected : MsoTriState {get;set}
- prop Value : String {get;set}

## WebCommandButton
- prop ActionURL : String {get;set}
- prop Application : Application {get}
- prop ButtonText : String {get;set}
- prop ButtonType : PbCommandButtonType {get;set}
- prop DataFileFormat : PbSubmitDataFormatType {get;set}
- prop DataFileName : String {get;set}
- prop DataRetrievalMethod : PbSubmitDataRetrievalMethodType {get;set}
- prop EmailAddress : String {get;set}
- prop EmailSubject : String {get;set}
- prop HiddenFields : WebHiddenFields {get}
- prop Parent : Object {get}
- prop PostFormData : MsoTriState {get;set}

## WebHiddenFields
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : String {get}
- prop Parent : Object {get}
- method Add(String Name, String Value) : Int32
- method Delete(Int32 Index) : Void
- method Name(Int32 Index) : String

## WebListBox
- prop Application : Application {get}
- prop ListBoxItems : WebListBoxItems {get}
- prop MultiSelect : MsoTriState {get;set}
- prop Parent : Object {get}
- prop ReturnDataLabel : String {get;set}

## WebListBoxItems
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : String {get}
- prop Parent : Object {get}
- method AddItem(String Item, Int32 Index, Boolean SelectState, String ItemValue) : Void
- method Delete(Int32 Index) : Void
- method Selected(Int32 Index, Boolean SelectState) : Void

## WebNavigationBarHyperlinks
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Index] : Hyperlink {get}
- prop Parent : Object {get}
- method Add(String Address, PbHlinkTargetType RelativePage, Int32 PageID, String TextToDisplay, Int32 Index) : Hyperlink
- method GetEnumerator() : IEnumerator

## WebNavigationBarSet
- prop Application : Application {get}
- prop AutoUpdate : Boolean {get;set}
- prop ButtonStyle : PbWizardNavBarButtonStyle {get;set}
- prop Design : PbWizardNavBarDesign {get;set}
- prop HorizontalAlignment : PbWizardNavBarAlignment {get;set}
- prop HorizontalButtonCount : Int32 {get;set}
- prop IsHorizontal : Boolean {get}
- prop Links : WebNavigationBarHyperlinks {get;set}
- prop Name : String {get;set}
- prop Parent : Object {get}
- prop ShowSelected : Boolean {get;set}
- method AddToEveryPage(Object Left, Object Top, Object Width) : ShapeRange
- method ChangeOrientation(PbNavBarOrientation Orientation) : Void
- method DeleteSetAndInstances() : Void

## WebNavigationBarSets
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Object Index] : WebNavigationBarSet {get}
- prop Parent : Object {get}
- method AddSet(String Name, PbWizardNavBarDesign Design, Boolean AutoUpdate) : WebNavigationBarSet
- method GetEnumerator() : IEnumerator

## WebOptionButton
- prop Application : Application {get}
- prop Parent : Object {get}
- prop ReturnDataLabel : String {get;set}
- prop Selected : MsoTriState {get;set}
- prop Value : String {get;set}

## WebOptions
- prop AlwaysSaveInDefaultEncoding : Boolean {get;set}
- prop Application : Application {get}
- prop EmailAsImg : Boolean {get;set}
- prop EnableIncrementalUpload : Boolean {get;set}
- prop Encoding : MsoEncoding {get;set}
- prop OrganizeInFolder : Boolean {get;set}
- prop Parent : Object {get}
- prop RelyOnVML : Boolean {get;set}
- prop ShowOnlyWebFonts : Boolean {get;set}

## WebPageOptions
- prop Application : Application {get}
- prop BackgroundSound : String {get;set}
- prop BackgroundSoundLoopCount : Int32 {get}
- prop BackgroundSoundLoopForever : Boolean {get}
- prop Description : String {get;set}
- prop IncludePageOnNewWebNavigationBars : Boolean {get;set}
- prop Keywords : String {get;set}
- prop Parent : Object {get}
- prop PublishFileName : String {get;set}
- method SetBackgroundSoundRepeat(Boolean RepeatForever, Int32 RepeatTimes) : Void

## WebTextBox
- prop Application : Application {get}
- prop DefaultText : String {get;set}
- prop EchoAsterisks : MsoTriState {get;set}
- prop Limit : Int32 {get;set}
- prop Parent : Object {get}
- prop RequiredControl : MsoTriState {get;set}
- prop ReturnDataLabel : String {get;set}

## Window
- prop Application : Application {get}
- prop Caption : String {get;set}
- prop Height : Int32 {get;set}
- prop Hwnd : Int32 {get}
- prop Left : Int32 {get;set}
- prop Parent : Object {get}
- prop Top : Int32 {get;set}
- prop Visible : Boolean {get;set}
- prop Width : Int32 {get;set}
- prop WindowState : PbWindowState {get;set}
- method Activate() : Void
- method Move(Int32 Left, Int32 Top) : Void
- method Resize(Int32 Width, Int32 Height) : Void

## Wizard
- prop Application : Application {get}
- prop ID : Int32 {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop Properties : WizardProperties {get}
- method SetId(Int32 ID) : Void

## WizardProperties
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : WizardProperty {get}
- prop Parent : Object {get}
- method FindPropertyById(Int32 ID) : WizardProperty
- method GetEnumerator() : IEnumerator

## WizardProperty
- prop Application : Application {get}
- prop CurrentValueId : Int32 {get;set}
- prop Enabled : Boolean {get}
- prop ID : Int32 {get}
- prop Name : String {get}
- prop Parent : Object {get}
- prop Values : WizardValues {get}

## WizardValue
- prop Application : Application {get}
- prop ID : Int32 {get}
- prop Name : String {get}
- prop Parent : Object {get}

## WizardValues
- prop Application : Application {get}
- prop Count : Int32 {get}
- prop Item[Int32 Item] : WizardValue {get}
- prop Parent : Object {get}
- method GetEnumerator() : IEnumerator

## WrapFormat
- prop Application : Application {get}
- prop DistanceAuto : MsoTriState {get;set}
- prop DistanceBottom : Object {get;set}
- prop DistanceLeft : Object {get;set}
- prop DistanceRight : Object {get;set}
- prop DistanceTop : Object {get;set}
- prop Parent : Object {get}
- prop Side : PbWrapSideType {get;set}
- prop Type : PbWrapType {get;set}
