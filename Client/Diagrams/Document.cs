// Document-level data saved in the .lf file: cable types and the drawing's title block.
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace Client.Diagrams;

// A user-defined cabling type with an assigned color. Cable types are per-diagram
// (saved in the .lf file); connections are colored by their assigned cable type.
public class CableType
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#e94560";       // on-screen line color
    // Label prefix used by auto-labeling, e.g. "DATA" -> "DATA-1", "DATA-2", ...
    public string Prefix { get; set; } = "";
    public string PartNumber { get; set; } = "";         // manufacturer part number
    public string CableColor { get; set; } = "";         // physical jacket color (text, e.g. "Blue")
}

// Drawing-sheet title block, modeled on a standard AV/telecom sheet: a vertical strip
// down the right edge of the page. Everything here is document-wide and saved in the
// .lf file; the drawing title and number are per-sheet (see Sheet), so each tab prints
// its own. Rendered into both the PDF and DXF exports when Enabled.
public class TitleBlockInfo
{
    public bool Enabled { get; set; }
    public string SheetSize { get; set; } = "11x17";   // 11x17 | A4 | Letter (all landscape)

    // Top block — your own company details.
    public string CompanyName { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyWeb { get; set; } = "";

    // Project block — printed rotated 90°, largest first, like a CAD sheet.
    public string Client { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Location { get; set; } = "";
    public string Discipline { get; set; } = "";

    // Label/value pairs under the revision grid.
    public string ProjectNumber { get; set; } = "";
    public string DrawnBy { get; set; } = "";
    public string CheckedBy { get; set; } = "";
    public string Date { get; set; } = "";

    public List<TitleBlockRevision> Revisions { get; set; } = new();
}

// One row of the title block's revision grid.
public class TitleBlockRevision
{
    public string Number { get; set; } = "";
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
}

// One tab of a multi-sheet document. Only the ACTIVE sheet is live in the diagram; the rest
// are parked as serialized JSON — the same round-trip undo snapshots and .lf saving use, so
// a parked sheet can't drift from a live one.
public class Sheet
{
    // An empty canvas, used wherever a sheet has no parked JSON yet.
    public const string EmptyJson = "{\"nodes\":[],\"links\":[]}";

    public string Name { get; set; } = "Sheet 1";
    public string? Json { get; set; }              // parked state (null while active)
    // Title block fields that differ per sheet — everything else is document-wide.
    public string DrawingTitle { get; set; } = "";
    public string DrawingNumber { get; set; } = "";
    public List<string> UndoStack { get; } = new();
    public List<string> RedoStack { get; } = new();
}

