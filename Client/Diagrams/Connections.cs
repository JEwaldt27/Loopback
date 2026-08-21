// Connections: the link model, its routing, and the nodes that decorate it.
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace Client.Diagrams;

public class ElbowLinkModel : LinkModel
{
    public double? MidX { get; set; }
    public string? LabelText { get; set; }
    public ConnectionLabelNode? LabelNode { get; set; }
    // Name of the assigned CableType (null = unassigned → neutral color).
    public string? CableTypeName { get; set; }
    // When true, the connection's long line is hidden and it renders as two "label
    // blocks" (one per end, carrying the connection's label) each tied to its port by a
    // short stub connection — a readability aid for busy diagrams. Still one logical
    // connection in the schedule/legend.
    public bool IsBroken { get; set; }
    // Runtime visuals for the broken state: the two tag blocks and their stub links.
    // Derived state — never serialized as first-class nodes/links; rebuilt from
    // IsBroken + the saved block positions on load/undo.
    public BreakTagNode? SrcTagNode { get; set; }
    public BreakTagNode? TgtTagNode { get; set; }
    public BreakStubLink? SrcStub { get; set; }
    public BreakStubLink? TgtStub { get; set; }
    // Bend positions parked while broken — vertex grab handles would render as stray
    // dots along the invisible line, so the bends are cleared on break and rebuilt
    // from here on rejoin. Serialized as the link's ordinary "vertices".
    public List<Point>? StashedVertices { get; set; }
    public ElbowLinkModel(PortModel source, PortModel target) : base(source, target) { }

    public static string ColorForType(string type) => type switch
    {
        "HDMI"    => "#4fc3f7",
        "SDI"     => "#81c784",
        "Audio"   => "#ffb74d",
        "Network" => "#ce93d8",
        "USB"     => "#f48fb1",
        "IR"      => "#80cbc4",
        "COM"     => "#fff176",
        _         => "#e94560",
    };
}

public class ElbowRouter : Blazor.Diagrams.Core.Routers.Router
{
    public override Point[] GetRoute(Blazor.Diagrams.Core.Diagram diagram, BaseLinkModel link)
    {
        if (link.Source.Model is not PortModel srcPort ||
            link.Target?.Model is not PortModel tgtPort)
            return Array.Empty<Point>();

        if (srcPort.Position == null || tgtPort.Position == null)
            return Array.Empty<Point>();

        double srcX = srcPort.Position.X + (srcPort.Size?.Width / 2 ?? 0);
        double srcY = srcPort.Position.Y + (srcPort.Size?.Height / 2 ?? 0);
        double tgtX = tgtPort.Position.X + (tgtPort.Size?.Width / 2 ?? 0);
        double tgtY = tgtPort.Position.Y + (tgtPort.Size?.Height / 2 ?? 0);

        var verts = link.Vertices;

        // Single vertex: simple H-V-H elbow
        if (verts.Count <= 1)
        {
            double midX = verts.Count == 1
                ? verts[0].Position.X
                : ((link as ElbowLinkModel)?.MidX ?? (srcX + tgtX) / 2);
            return new[] { new Point(midX, srcY), new Point(midX, tgtY) };
        }

        // Multiple vertices: H-V-H-V-...-H
        // verts[0].X = first vertical segment X (Y ignored — kept centered by SetupElbowLink)
        // verts[i].Y (i>0) = Y of horizontal segment between vertical i-1 and vertical i
        // verts[i].X (i>0) = X of vertical segment i
        var pts = new List<Point> { new Point(verts[0].Position.X, srcY) };
        for (int i = 0; i < verts.Count - 1; i++)
        {
            pts.Add(new Point(verts[i].Position.X, verts[i + 1].Position.Y));
            pts.Add(new Point(verts[i + 1].Position.X, verts[i + 1].Position.Y));
        }
        pts.Add(new Point(verts[^1].Position.X, tgtY));
        return pts.ToArray();
    }
}

// Plain HTML node (not an SVG foreignObject) so it renders correctly in html2canvas-based
// PDF export, unlike the diagramming library's built-in link labels. Positioned at the
// link's midpoint when created; the user can drag it independently afterward, same as
// TextNode/BoxNode/LineNode annotations.
public class ConnectionLabelNode : NodeModel
{
    public string Text { get; set; } = "";
    public ElbowLinkModel? OwnerLink { get; set; }
    // Clockwise rotation in 90° increments (0/90/180/270), applied as a CSS transform
    // in the widget. Persisted per-label so it survives save/open and undo/redo.
    public int Rotation { get; set; } = 0;
    public ConnectionLabelNode(Point position) : base(position) { }
}

// A broken connection's end tag: a small free-standing "label block" node tied to its
// port by a BreakStubLink. It's an ordinary diagram node, so dragging, selection,
// undo capture, and PDF/DXF bounds all come from the diagram engine — the device
// block itself is never touched. Recreated from the owning link's data on load.
public class BreakTagNode : NodeModel
{
    public string Text { get; set; } = "";
    public string Color { get; set; } = "#808080";   // border color = cable color
    public ElbowLinkModel? OwnerLink { get; set; }
    public bool IsSource { get; set; }               // which end of the owner this tag marks
    public BreakTagNode(Point position) : base(position) { }
}

// The short visible connection drawn from a port to its BreakTagNode. ElbowRouter
// ignores links whose target isn't a port, so these render as plain straight lines.
// Locked — the tag block is the thing users grab, not the stub.
public class BreakStubLink : LinkModel
{
    public ElbowLinkModel? Owner { get; set; }
    public BreakStubLink(Blazor.Diagrams.Core.Anchors.Anchor source, Blazor.Diagrams.Core.Anchors.Anchor target)
        : base(source, target) { }
}
