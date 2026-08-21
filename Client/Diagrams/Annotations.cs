// Free-standing things drawn on the canvas that aren't devices or cables.
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace Client.Diagrams;

public class BoxNode : NodeModel
{
    public string BorderColor { get; set; } = "#e94560";
    public BoxNode(Point position) : base(position)
    {
        Size = new Blazor.Diagrams.Core.Geometry.Size(160, 100);
    }
}

public class LineNode : NodeModel
{
    public Point Start { get; set; } = new Point(0, 0);
    public Point End { get; set; } = new Point(150, 0);
    public string StrokeColor { get; set; } = "#333333";

    public LineNode(Point position) : base(position)
    {
        Size = new Blazor.Diagrams.Core.Geometry.Size(150, 1);
    }
}

public class TextNode : NodeModel
{
    public string Text { get; set; } = "Text";
    public int FontSize { get; set; } = 16;
    public string Color { get; set; } = "#222222";
    public bool Editing { get; set; } = false;

    public TextNode(Point position) : base(position) { }
}

public class LegendNode : NodeModel
{
    public List<(string Name, string Color, string PartNumber, string CableColor)> Entries { get; set; } = new();
    public LegendNode(Point position) : base(position) { }
}

// A schedule printed on the drawing itself: the same data as the CSV exports, as a
// table you can place, drag, and export with the sheet. Deliberately holds NO rows —
// only what to show and how wide to cast the net. The widget asks Home for the data on
// every render, so the table can't drift out of step with the diagram under it.
public class ScheduleNode : NodeModel
{
    public ScheduleKind Kind { get; set; }
    public bool AllSheets { get; set; }
    public ScheduleNode(Point position) : base(position) { }
}

public enum ScheduleKind { Cable, Device, CableCount }
