// AutoCAD DXF (R12 / AC1009) generation. Pure string building over a finished diagram —
// no component state and no JS interop — so it lives here rather than in Home.razor.
using System.Globalization;
using Blazor.Diagrams;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using Client.Diagrams;

namespace Client.Export;

public static class DxfWriter
{
    // Text style written into the DXF and referenced by every TEXT entity. The font is the
    // file name CAD resolves at open time, so it must exist on the machine opening the
    // drawing — Arial ships with Windows and is mapped on macOS/Linux CAD too. Swap for
    // another .ttf (or an .shx) here if a project standard calls for something else.
    internal const string DxfTextStyle = "LOOPBACK";
    internal const string DxfTextFont = "arial.ttf";

    // Cable type name -> DXF layer name. R12 layer names are uppercase, max 31 characters,
    // and can't contain spaces or < > / \ " : ; ? * | = ' — so anything outside
    // letters/digits/$-_ becomes an underscore. The CABLE- prefix keeps them grouped
    // together in CAD's layer list, next to each other and away from NODES/ANNOTATIONS.
    internal static string DxfLayerForCable(string cableTypeName)
    {
        var cleaned = new string((cableTypeName ?? "").Trim().ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c is '$' or '-' or '_' ? c : '_')
            .ToArray()).Trim('_');
        if (cleaned.Length == 0) cleaned = "UNNAMED";
        if (cleaned.Length > 25) cleaned = cleaned[..25];   // 6 for "CABLE-" + 25 = the 31 cap
        return "CABLE-" + cleaned;
    }

    // Nearest AutoCAD Color Index for a #rrggbb cable color. R12 predates true color
    // (code 420, R2000+), so layer colors come from the ACI palette; this matches against
    // the six primaries plus white and two grays. It's only a starting point — the reason
    // each cable type gets its own layer is so these can be restyled in CAD.
    internal static int DxfAciColor(string? hex)
    {
        if (hex == null || hex.Length != 7 || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return 7;

        (int Aci, int R, int G, int B)[] palette =
        {
            (1, 255, 0, 0), (2, 255, 255, 0), (3, 0, 255, 0), (4, 0, 255, 255),
            (5, 0, 0, 255), (6, 255, 0, 255), (7, 255, 255, 255),
            (8, 128, 128, 128), (9, 192, 192, 192)
        };

        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        int best = 7;
        long bestDistance = long.MaxValue;
        foreach (var (aci, pr, pg, pb) in palette)
        {
            long d = (long)(r - pr) * (r - pr) + (long)(g - pg) * (g - pg) + (long)(b - pb) * (b - pb);
            if (d < bestDistance) { bestDistance = d; best = aci; }
        }
        return best;
    }

    public static string Build(
        BlazorDiagram diagram,
        IReadOnlyList<CableType> cableTypes,
        TitleBlockInfo titleBlock,
        Sheet activeSheet,
        string diagramTitle,
        Func<ScheduleNode, (string Title, List<string> Headers, List<List<string>> Rows)> scheduleTable)
    {
        var sb = new System.Text.StringBuilder();

        double scale = 0.3;

        // Running extents of everything emitted, used to size the title-block sheet around
        // the finished drawing (DXF is model space — there's no page until we draw one).
        double exMinX = double.MaxValue, exMinY = double.MaxValue;
        double exMaxX = double.MinValue, exMaxY = double.MinValue;
        void Extend(double x, double y)
        {
            if (x < exMinX) exMinX = x;
            if (y < exMinY) exMinY = y;
            if (x > exMaxX) exMaxX = x;
            if (y > exMaxY) exMaxY = y;
        }

        // R12 (AC1009) is the most widely-compatible DXF flavor: unlike R2000+ (AC1015)
        // it needs no per-entity subclass markers (100 AcDbEntity/AcDbLine/...) or handles,
        // which this writer doesn't emit. Declaring AC1015 without them makes AutoCAD throw
        // a DXF read error and open a blank drawing.
        sb.AppendLine("0\nSECTION");
        sb.AppendLine("2\nHEADER");
        sb.AppendLine("9\n$ACADVER");
        sb.AppendLine("1\nAC1009");
        sb.AppendLine("0\nENDSEC");

        // Each cabling type used on this sheet gets its own layer, so colors, lineweights,
        // and visibility can be set per cable type in CAD (the layer's initial color is the
        // nearest ACI match to the on-screen color). Types with no connections on this sheet
        // are skipped, matching how the legend only lists what's actually used. Connections
        // with no cable type fall back to the generic CONNECTIONS layer.
        var cableLayers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var takenLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "0", "NODES", "CONNECTIONS", "ANNOTATIONS", "LEGEND", "SCHEDULES", "TITLEBLOCK" };
        foreach (var ctName in diagram.Links.OfType<ElbowLinkModel>()
                     .Where(l => l.Source.Model is PortModel && l.Target?.Model is PortModel
                                 && !string.IsNullOrWhiteSpace(l.CableTypeName))
                     .Select(l => l.CableTypeName!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var baseName = DxfLayerForCable(ctName);
            var layer = baseName;
            // Two type names can sanitize to the same layer ("Cat 6" / "Cat-6") — suffix dupes.
            for (int i = 2; !takenLayers.Add(layer); i++)
                layer = $"{baseName[..Math.Min(baseName.Length, 28)]}-{i}";
            cableLayers[ctName] = layer;
        }

        var layers = new List<(string Name, int Color)>
        {
            ("NODES", 7), ("CONNECTIONS", 7), ("ANNOTATIONS", 7), ("LEGEND", 7), ("SCHEDULES", 7), ("TITLEBLOCK", 7)
        };
        foreach (var (typeName, layerName) in cableLayers)
            layers.Add((layerName, DxfAciColor(cableTypes.FirstOrDefault(c => c.Name == typeName)?.Color)));

        sb.AppendLine("0\nSECTION");
        sb.AppendLine("2\nTABLES");
        sb.AppendLine("0\nTABLE");
        sb.AppendLine("2\nLAYER");
        sb.AppendLine($"70\n{layers.Count}");
        foreach (var (layerName, layerColor) in layers)
        {
            sb.AppendLine("0\nLAYER");
            sb.AppendLine($"2\n{layerName}");
            sb.AppendLine("70\n0");
            sb.AppendLine($"62\n{layerColor}");
            sb.AppendLine("6\nCONTINUOUS");
        }
        sb.AppendLine("0\nENDTAB");

        // Text style. Without a STYLE table (and a 7 group on each TEXT naming it), CAD
        // falls back to the "Standard" style, whose default font is the txt.shx monoline
        // stick font — legible but ugly. Group code 3 is the primary font file; naming a
        // TrueType file there ("arial.ttf") gets a normal-looking font instead. A custom
        // style name is used rather than redefining "Standard", so importing this drawing
        // into an existing one doesn't restyle that drawing's own text.
        sb.AppendLine("0\nTABLE");
        sb.AppendLine("2\nSTYLE");
        sb.AppendLine("70\n1");
        sb.AppendLine("0\nSTYLE");
        sb.AppendLine($"2\n{DxfTextStyle}");
        sb.AppendLine("70\n0");
        sb.AppendLine("40\n0.0");   // 0 = height comes from each TEXT entity, not fixed here
        sb.AppendLine("41\n1.0");   // width factor
        sb.AppendLine("50\n0.0");   // oblique angle
        sb.AppendLine("71\n0");
        sb.AppendLine("42\n2.5");
        sb.AppendLine($"3\n{DxfTextFont}");
        sb.AppendLine("4\n");       // bigfont file: none
        sb.AppendLine("0\nENDTAB");

        sb.AppendLine("0\nENDSEC");

        sb.AppendLine("0\nSECTION");
        sb.AppendLine("2\nENTITIES");

        var portPositions = new Dictionary<string, (double x, double y)>();

        // DXF requires a '.' decimal separator regardless of the user's locale, so format
        // every coordinate with the invariant culture (a comma-decimal locale would
        // otherwise emit "12,34" and corrupt the file).
        static string F(double v) => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

        void DxfLine(double x1, double y1, double x2, double y2, string layer = "NODES")
        {
            if (layer != "TITLEBLOCK") { Extend(x1, y1); Extend(x2, y2); }
            sb.AppendLine("0\nLINE");
            sb.AppendLine($"8\n{layer}");
            sb.AppendLine($"10\n{F(x1)}");
            sb.AppendLine($"20\n{F(y1)}");
            sb.AppendLine("30\n0.0");
            sb.AppendLine($"11\n{F(x2)}");
            sb.AppendLine($"21\n{F(y2)}");
            sb.AppendLine("31\n0.0");
        }

        // Filled quad, used for the legend's color swatches.
        void DxfSolid(double x1, double y1, double x2, double y2, string layer)
        {
            Extend(x1, y1); Extend(x2, y2);
            sb.AppendLine("0\nSOLID");
            sb.AppendLine($"8\n{layer}");
            // SOLID corner order is bottom-left, bottom-right, TOP-LEFT, TOP-RIGHT — the
            // third and fourth points are swapped versus a normal quad winding. Ordering
            // them the "obvious" way renders an hourglass instead of a rectangle.
            sb.AppendLine($"10\n{F(x1)}"); sb.AppendLine($"20\n{F(y1)}"); sb.AppendLine("30\n0.0");
            sb.AppendLine($"11\n{F(x2)}"); sb.AppendLine($"21\n{F(y1)}"); sb.AppendLine("31\n0.0");
            sb.AppendLine($"12\n{F(x1)}"); sb.AppendLine($"22\n{F(y2)}"); sb.AppendLine("32\n0.0");
            sb.AppendLine($"13\n{F(x2)}"); sb.AppendLine($"23\n{F(y2)}"); sb.AppendLine("33\n0.0");
        }

        void DxfText(double tx, double ty, string text, double height = 2.5, string layer = "NODES", int halign = 0, double rotation = 0)
        {
            if (layer != "TITLEBLOCK") Extend(tx, ty);
            sb.AppendLine("0\nTEXT");
            sb.AppendLine($"8\n{layer}");
            sb.AppendLine($"10\n{F(tx)}");
            sb.AppendLine($"20\n{F(ty)}");
            sb.AppendLine("30\n0.0");
            sb.AppendLine($"40\n{F(height)}");
            sb.AppendLine($"1\n{text ?? ""}");
            if (rotation != 0)
                sb.AppendLine($"50\n{F(rotation)}");
            sb.AppendLine($"7\n{DxfTextStyle}");
            sb.AppendLine($"72\n{halign}");
            sb.AppendLine($"11\n{F(tx)}");
            sb.AppendLine($"21\n{F(ty)}");
            sb.AppendLine("31\n0.0");
        }

        (double x, double y) PolylineMidpoint(List<(double x, double y)> pts)
        {
            double total = 0;
            for (int i = 0; i < pts.Count - 1; i++)
                total += Math.Sqrt(Math.Pow(pts[i + 1].x - pts[i].x, 2) + Math.Pow(pts[i + 1].y - pts[i].y, 2));

            double target = total / 2;
            double covered = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double segLen = Math.Sqrt(Math.Pow(pts[i + 1].x - pts[i].x, 2) + Math.Pow(pts[i + 1].y - pts[i].y, 2));
                if (segLen <= 0) continue;
                if (covered + segLen >= target)
                {
                    double t = (target - covered) / segLen;
                    return (pts[i].x + (pts[i + 1].x - pts[i].x) * t, pts[i].y + (pts[i + 1].y - pts[i].y) * t);
                }
                covered += segLen;
            }
            return pts[^1];
        }

        foreach (var node in diagram.Nodes.OfType<LineFlowNode>())
        {
            var leftPorts = node.Ports.OfType<LineFlowPort>()
                .Where(p => p.Alignment == PortAlignment.Left).ToList();
            var rightPorts = node.Ports.OfType<LineFlowPort>()
                .Where(p => p.Alignment == PortAlignment.Right).ToList();

            int maxPorts = Math.Max(leftPorts.Count, rightPorts.Count);
            double portHeight = 16;
            double titleHeight = 20;
            double padding = 8;
            double nodeHeight = titleHeight + (maxPorts * portHeight) + padding;
            double nodeWidth = 180;

            double x = node.Position.X * scale;
            double y = -(node.Position.Y * scale);
            double w = nodeWidth * scale;
            double h = nodeHeight * scale;
            double sph = portHeight * scale;
            double sth = titleHeight * scale;

            DxfLine(x, y, x + w, y);
            DxfLine(x + w, y, x + w, y - h);
            DxfLine(x + w, y - h, x, y - h);
            DxfLine(x, y - h, x, y);
            DxfLine(x, y - sth, x + w, y - sth);
            // Tag first when set, so two of the same model are distinguishable in CAD too.
            var nodeTitle = string.IsNullOrWhiteSpace(node.Label) ? node.Title : $"[{node.Label}] {node.Title}";
            DxfText(x + w / 2, y - sth + sth * 0.3, nodeTitle, 2.5, "NODES", 1);

            for (int i = 0; i < leftPorts.Count; i++)
            {
                double py = y - sth - (i * sph) - (sph * 0.5);
                double px = x;
                DxfText(px + 1, py - 1, leftPorts[i].Definition.Name, 1.8, "NODES", 0);
                portPositions[leftPorts[i].Id] = (px, py);
            }

            for (int i = 0; i < rightPorts.Count; i++)
            {
                double py = y - sth - (i * sph) - (sph * 0.5);
                double px = x + w;
                DxfText(px - 1, py - 1, rightPorts[i].Definition.Name, 1.8, "NODES", 2);
                portPositions[rightPorts[i].Id] = (px, py);
            }
        }

        foreach (var link in diagram.Links)
        {
            if (link.Source.Model is not PortModel sourcePort) continue;
            if (link.Target?.Model is not PortModel targetPort) continue;
            if (!portPositions.TryGetValue(sourcePort.Id, out var srcPos)) continue;
            if (!portPositions.TryGetValue(targetPort.Id, out var tgtPos)) continue;

            var verts = link.Vertices;
            List<(double x, double y)> pts;

            // Vertices/label positions are in screen/port coordinate space.
            // Convert to DXF space using source port as reference:
            //   dxfX = (screenX - srcScreenX) * scale + srcPos.x
            //   dxfY = -(screenY - srcScreenY) * scale + srcPos.y  (Y axis is flipped in DXF)
            double srcScreenX = (sourcePort.Position?.X ?? 0) + (sourcePort.Size?.Width / 2 ?? 0);
            double srcScreenY = (sourcePort.Position?.Y ?? 0) + (sourcePort.Size?.Height / 2 ?? 0);

            (double x, double y) ToDxf(double sx, double sy) =>
                ((sx - srcScreenX) * scale + srcPos.x,
                 -(sy - srcScreenY) * scale + srcPos.y);

            if (verts.Count == 0)
            {
                // No vertices: centered elbow
                double midX = (srcPos.x + tgtPos.x) / 2;
                pts = new List<(double x, double y)> { srcPos, (midX, srcPos.y), (midX, tgtPos.y), tgtPos };
            }
            else if (verts.Count == 1)
            {
                // Simple H-V-H elbow using vertex X
                var (vx, _) = ToDxf(verts[0].Position.X, verts[0].Position.Y);
                pts = new List<(double x, double y)> { srcPos, (vx, srcPos.y), (vx, tgtPos.y), tgtPos };
            }
            else
            {
                // Multi-vertex: H-V-H-V-...-H matching the router's path
                var dv = verts.Select(v => ToDxf(v.Position.X, v.Position.Y)).ToList();

                pts = new List<(double x, double y)>();
                pts.Add(srcPos);
                pts.Add((dv[0].x, srcPos.y));
                for (int i = 0; i < dv.Count - 1; i++)
                {
                    pts.Add((dv[i].x, dv[i + 1].y));
                    pts.Add((dv[i + 1].x, dv[i + 1].y));
                }
                pts.Add((dv[^1].x, tgtPos.y));
                pts.Add(tgtPos);
            }

            // Everything belonging to this cable — line, label, break stubs and tags — goes
            // on its cable type's layer; unassigned connections stay on CONNECTIONS.
            var cableLayer = link is ElbowLinkModel typed && typed.CableTypeName != null
                             && cableLayers.TryGetValue(typed.CableTypeName, out var cl)
                ? cl : "CONNECTIONS";

            if (link is ElbowLinkModel brokenLink && brokenLink.IsBroken)
            {
                // Broken: a stub from each port to its tag block, tag text at the block —
                // mirroring the on-screen layout (blocks may have been dragged anywhere).
                var s = pts[0];
                var t = pts[^1];
                var tag = brokenLink.LabelText ?? "";
                if (brokenLink.SrcTagNode != null)
                {
                    var p = ToDxf(brokenLink.SrcTagNode.Position.X, brokenLink.SrcTagNode.Position.Y);
                    DxfLine(s.x, s.y, p.x, p.y, cableLayer);
                    DxfText(p.x, p.y, tag, 2.0, cableLayer, 0);
                }
                if (brokenLink.TgtTagNode != null)
                {
                    var p = ToDxf(brokenLink.TgtTagNode.Position.X, brokenLink.TgtTagNode.Position.Y);
                    DxfLine(t.x, t.y, p.x, p.y, cableLayer);
                    DxfText(p.x, p.y, tag, 2.0, cableLayer, 0);
                }
            }
            else
            {
                for (int i = 0; i < pts.Count - 1; i++)
                    DxfLine(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, cableLayer);

                if (link is ElbowLinkModel elbowLink && !string.IsNullOrEmpty(elbowLink.LabelText))
                {
                    var labelPos = elbowLink.LabelNode != null
                        ? ToDxf(elbowLink.LabelNode.Position.X, elbowLink.LabelNode.Position.Y)
                        : PolylineMidpoint(pts);
                    // Screen rotation is clockwise; the DXF Y-axis is flipped, which turns that
                    // into the counterclockwise-positive angle DXF's group code 50 expects.
                    double labelRot = elbowLink.LabelNode?.Rotation ?? 0;
                    DxfText(labelPos.x, labelPos.y, elbowLink.LabelText, 2.0, cableLayer, 1, labelRot);
                }
            }
        }

        foreach (var box in diagram.Nodes.OfType<BoxNode>())
        {
            double x = box.Position.X * scale;
            double y = -(box.Position.Y * scale);
            double w = (box.Size?.Width ?? 160) * scale;
            double h = (box.Size?.Height ?? 100) * scale;

            DxfLine(x, y, x + w, y, "ANNOTATIONS");
            DxfLine(x + w, y, x + w, y - h, "ANNOTATIONS");
            DxfLine(x + w, y - h, x, y - h, "ANNOTATIONS");
            DxfLine(x, y - h, x, y, "ANNOTATIONS");
        }

        foreach (var line in diagram.Nodes.OfType<LineNode>())
        {
            double x1 = (line.Position.X + line.Start.X) * scale;
            double y1 = -((line.Position.Y + line.Start.Y) * scale);
            double x2 = (line.Position.X + line.End.X) * scale;
            double y2 = -((line.Position.Y + line.End.Y) * scale);

            DxfLine(x1, y1, x2, y2, "ANNOTATIONS");
        }

        foreach (var text in diagram.Nodes.OfType<TextNode>())
        {
            double x = text.Position.X * scale;
            double y = -(text.Position.Y * scale);

            DxfText(x, y, text.Text, Math.Max(text.FontSize * scale, 1.0), "ANNOTATIONS", 0);
        }

        // Legend, redrawn as a real CAD table (the on-screen one is an HTML table, which
        // has no DXF equivalent). Column widths are measured from the actual content so
        // nothing overlaps, and the Part #/Color columns appear only when in use — same
        // rule the on-screen legend follows.
        foreach (var legend in diagram.Nodes.OfType<LegendNode>())
        {
            if (legend.Entries.Count == 0) continue;

            const double rowH = 5.5, textH = 2.0, cellPad = 1.6, swatchW = 5.0, swatchH = 2.6;
            // Generous per-character advance: real Arial is narrower, but over-estimating
            // only pads the cells, whereas under-estimating runs one column into the next.
            static double TextW(string? s, double h) => (s ?? "").Length * h * 0.78;

            bool anyPart = legend.Entries.Any(e => !string.IsNullOrWhiteSpace(e.PartNumber));
            bool anyColor = legend.Entries.Any(e => !string.IsNullOrWhiteSpace(e.CableColor));

            // Column widths: widest of header vs. content, plus padding on both sides.
            double Col(string header, Func<(string Name, string Color, string PartNumber, string CableColor), string?> pick)
                => Math.Max(TextW(header, textH), legend.Entries.Max(e => TextW(pick(e), textH))) + cellPad * 2;

            var colWidths = new List<double> { swatchW + cellPad * 2, Col("Cable Type", e => e.Name) };
            if (anyPart) colWidths.Add(Col("Part #", e => e.PartNumber));
            if (anyColor) colWidths.Add(Col("Color", e => e.CableColor));

            // Cell boundaries — the grid is drawn from these, so lines and text can't disagree.
            double lx = legend.Position.X * scale;
            double ly = -(legend.Position.Y * scale);   // top edge; DXF Y grows upward
            var colX = new List<double> { lx };
            foreach (var w in colWidths) colX.Add(colX[^1] + w);
            double right = colX[^1];

            int rowCount = legend.Entries.Count;
            double bottom = ly - rowH * (rowCount + 1);   // +1 for the header row
            int nameCol = 1;
            int partCol = anyPart ? 2 : -1;
            int colorCol = anyColor ? (anyPart ? 3 : 2) : -1;

            // Full grid: a horizontal rule at every row boundary, a vertical at every
            // column boundary — including the outer edges, which form the frame.
            for (int r = 0; r <= rowCount + 1; r++)
            {
                double gy = ly - rowH * r;
                DxfLine(lx, gy, right, gy, "LEGEND");
            }
            foreach (var gx in colX)
                DxfLine(gx, ly, gx, bottom, "LEGEND");

            // Header row
            double headerBaseline = ly - rowH + (rowH - textH) / 2;
            DxfText(colX[nameCol] + cellPad, headerBaseline, "Cable Type", textH, "LEGEND", 0);
            if (anyPart) DxfText(colX[partCol] + cellPad, headerBaseline, "Part #", textH, "LEGEND", 0);
            if (anyColor) DxfText(colX[colorCol] + cellPad, headerBaseline, "Color", textH, "LEGEND", 0);

            for (int i = 0; i < rowCount; i++)
            {
                var entry = legend.Entries[i];
                double rowBottom = ly - rowH * (i + 2);
                double baseline = rowBottom + (rowH - textH) / 2;

                // The swatch sits on its cable type's own layer, so recoloring that layer
                // in CAD updates the legend key to match instead of leaving it stale.
                var swatchLayer = cableLayers.TryGetValue(entry.Name ?? "", out var sl) ? sl : "LEGEND";
                double sx = colX[0] + cellPad, sy = rowBottom + (rowH - swatchH) / 2;
                DxfSolid(sx, sy, sx + swatchW, sy + swatchH, swatchLayer);
                // Outline on LEGEND so a pale swatch is still visible against white paper.
                DxfLine(sx, sy, sx + swatchW, sy, "LEGEND");
                DxfLine(sx + swatchW, sy, sx + swatchW, sy + swatchH, "LEGEND");
                DxfLine(sx + swatchW, sy + swatchH, sx, sy + swatchH, "LEGEND");
                DxfLine(sx, sy + swatchH, sx, sy, "LEGEND");

                DxfText(colX[nameCol] + cellPad, baseline, entry.Name, textH, "LEGEND", 0);
                if (anyPart) DxfText(colX[partCol] + cellPad, baseline, entry.PartNumber, textH, "LEGEND", 0);
                if (anyColor) DxfText(colX[colorCol] + cellPad, baseline, entry.CableColor, textH, "LEGEND", 0);
            }
        }

        // Schedules placed on the sheet, redrawn as real CAD tables on their own SCHEDULES
        // layer. Rows come from the same BuildScheduleTable the on-screen widget uses, so
        // the CAD table and the drawing can't disagree.
        foreach (var schedule in diagram.Nodes.OfType<ScheduleNode>())
        {
            var (title, headers, rows) = scheduleTable(schedule);
            if (headers.Count == 0) continue;

            const double rowH = 4.6, textH = 1.8, cellPad = 1.4;
            // Generous per-character advance: real Arial is narrower, but over-estimating
            // only pads the cells, whereas under-estimating runs one column into the next.
            static double TextW(string? str, double h) => (str ?? "").Length * h * 0.78;

            // Column widths: widest of header vs. any cell in that column, plus padding.
            var colWidths = headers.Select((h, c) =>
                Math.Max(TextW(h, textH),
                         rows.Count == 0 ? 0 : rows.Max(r => c < r.Count ? TextW(r[c], textH) : 0))
                + cellPad * 2).ToList();

            double tx = schedule.Position.X * scale;
            double ty = -(schedule.Position.Y * scale);   // top edge; DXF Y grows upward
            var colX = new List<double> { tx };
            foreach (var w in colWidths) colX.Add(colX[^1] + w);
            double right = colX[^1];

            // Title sits above the frame, like the on-screen table's caption.
            DxfText(tx, ty + cellPad, title, textH * 1.2, "SCHEDULES", 0);

            double bottom = ty - rowH * (rows.Count + 1);   // +1 for the header row
            for (int r = 0; r <= rows.Count + 1; r++)
            {
                double gy = ty - rowH * r;
                DxfLine(tx, gy, right, gy, "SCHEDULES");
            }
            foreach (var gx in colX)
                DxfLine(gx, ty, gx, bottom, "SCHEDULES");

            double headerBaseline = ty - rowH + (rowH - textH) / 2;
            for (int c = 0; c < headers.Count; c++)
                DxfText(colX[c] + cellPad, headerBaseline, headers[c], textH, "SCHEDULES", 0);

            for (int r = 0; r < rows.Count; r++)
            {
                double baseline = ty - rowH * (r + 2) + (rowH - textH) / 2;
                for (int c = 0; c < headers.Count && c < rows[r].Count; c++)
                    DxfText(colX[c] + cellPad, baseline, rows[r][c], textH, "SCHEDULES", 0);
            }

            // Keep the table inside the drawing extents so the title block frames it too.
            exMinX = Math.Min(exMinX, tx); exMaxX = Math.Max(exMaxX, right);
            exMinY = Math.Min(exMinY, bottom); exMaxY = Math.Max(exMaxY, ty + cellPad + textH);
        }

        // Title block: a drawing sheet laid out around the finished geometry. DXF is model
        // space with no notion of paper, so the sheet is sized to the chosen aspect ratio
        // and scaled up until the content fits inside its drawing area, then centered there.
        if (titleBlock.Enabled && exMaxX > exMinX && exMaxY > exMinY)
        {
            var sheet = activeSheet;
            double paperW = titleBlock.SheetSize switch
            {
                "11x17" => 431.8, "Letter" => 279.4, _ => 297.0     // landscape mm
            };
            double paperH = titleBlock.SheetSize switch
            {
                "11x17" => 279.4, "Letter" => 215.9, _ => 210.0
            };
            double aspect = paperW / paperH;

            const double stripFrac = 0.113, marginFrac = 0.02;
            double drawWFrac = 1 - stripFrac - marginFrac * 2;
            double drawHFrac = 1 - marginFrac * 2;

            double contentW = (exMaxX - exMinX) * 1.06;   // breathing room
            double contentH = (exMaxY - exMinY) * 1.06;
            double sheetW = Math.Max(contentW / drawWFrac, contentH / drawHFrac * aspect);
            double sheetH = sheetW / aspect;

            // Centre the drawing area on the content.
            double contentCX = (exMinX + exMaxX) / 2, contentCY = (exMinY + exMaxY) / 2;
            double left = contentCX - sheetW * marginFrac - sheetW * drawWFrac / 2;
            double bottom = contentCY - sheetH * marginFrac - sheetH * drawHFrac / 2;
            double right = left + sheetW, top = bottom + sheetH;

            const string TB = "TITLEBLOCK";
            double t = sheetH * 0.011;                     // base text height
            double pad = sheetH * 0.008;
            void Rect(double ax, double ay, double bx2, double by2)
            {
                DxfLine(ax, ay, bx2, ay, TB); DxfLine(bx2, ay, bx2, by2, TB);
                DxfLine(bx2, by2, ax, by2, TB); DxfLine(ax, by2, ax, ay, TB);
            }

            // Double border
            Rect(left, bottom, right, top);
            double ib = sheetH * 0.006;
            Rect(left + ib, bottom + ib, right - ib, top - ib);

            double sx = right - ib - sheetW * stripFrac;   // strip left edge
            double sTop = top - ib, sBot = bottom + ib, sRight = right - ib;
            DxfLine(sx, sBot, sx, sTop, TB);
            void Rule(double ry) => DxfLine(sx, ry, sRight, ry, TB);

            double y = sTop;

            // Logo box (intentionally empty) + company details, right-aligned
            double logoH = sheetH * 0.10;
            Rect(sx + pad, y - logoH + pad, sRight - pad, y - pad);
            y -= logoH;
            foreach (var lineText in new[] { titleBlock.CompanyName, titleBlock.CompanyAddress,
                                             titleBlock.CompanyPhone, titleBlock.CompanyWeb })
            {
                if (string.IsNullOrWhiteSpace(lineText)) continue;
                y -= t * 1.6;
                DxfText(sRight - pad, y, lineText, t * 0.8, TB, 2);
            }
            y -= pad; Rule(y);

            // Everything from the revision grid down is ANCHORED TO THE BOTTOM of the strip,
            // matching the PDF and how a real sheet reads: the drawing number sits in a short
            // box in the bottom corner. Worked out first so the project and stamp blocks above
            // can divide up exactly the space that's actually left.
            const int revRows = 6;
            double revH = sheetH * 0.022;
            double numberBoxH = sheetH * 0.072, titleBoxH = sheetH * 0.072;
            double pairLead = t * 1.7;
            double pairsH = pairLead * 4 + pad;

            double numberTop = sBot + numberBoxH;
            double titleTop = numberTop + titleBoxH;
            double pairsTop = titleTop + pairsH;
            double revTop = Math.Min(pairsTop + revRows * revH, y - sheetH * 0.06);

            // Project block — rotated 90°, largest first. Takes the larger share of the space
            // between the company details and the revision grid; the stamp box gets the rest.
            double projBottom = y - (y - revTop) * 0.70;
            double px = sx + pad + t * 1.4;
            foreach (var (text, size) in new[]
            {
                (titleBlock.Client, t * 1.9), (titleBlock.ProjectName, t * 1.4),
                (titleBlock.Location, t * 1.1), (titleBlock.Discipline, t * 1.1)
            })
            {
                if (!string.IsNullOrWhiteSpace(text))
                    DxfText(px, projBottom + pad, text, size, TB, 0, 90);
                px += size * 1.5;
            }
            Rule(projBottom);

            // Stamp box: from the project block down to the revision grid.
            DxfText(sx + pad, projBottom - t * 1.6, "STAMP", t * 0.85, TB, 0);
            Rule(revTop);

            // Revisions grid
            double numW = sheetW * 0.014, dateW = sheetW * 0.030;
            for (int r = 1; r <= revRows; r++) Rule(revTop - r * revH);
            double revBottom = revTop - revRows * revH;
            DxfLine(sx + numW, revBottom, sx + numW, revTop, TB);
            DxfLine(sx + numW + dateW, revBottom, sx + numW + dateW, revTop, TB);
            DxfText(sx + pad * 0.6, revTop - revH + pad * 0.6, "#", t * 0.75, TB, 0);
            DxfText(sx + numW + pad * 0.6, revTop - revH + pad * 0.6, "DATE", t * 0.75, TB, 0);
            DxfText(sx + numW + dateW + pad * 0.6, revTop - revH + pad * 0.6, "DESCRIPTION", t * 0.75, TB, 0);
            for (int i = 0; i < titleBlock.Revisions.Count && i < revRows - 1; i++)
            {
                var rev = titleBlock.Revisions[i];
                double ry = revTop - revH * (i + 2) + pad * 0.6;
                DxfText(sx + pad * 0.6, ry, rev.Number, t * 0.8, TB, 0);
                DxfText(sx + numW + pad * 0.6, ry, rev.Date, t * 0.8, TB, 0);
                DxfText(sx + numW + dateW + pad * 0.6, ry, rev.Description, t * 0.8, TB, 0);
            }

            // Label / value pairs
            double py = pairsTop;
            foreach (var (label, value) in new[]
            {
                ("PROJECT NUM.", titleBlock.ProjectNumber), ("DRAWN BY", titleBlock.DrawnBy),
                ("CHECKED BY", titleBlock.CheckedBy), ("DATE", titleBlock.Date)
            })
            {
                py -= pairLead;
                DxfText(sx + pad, py, label, t * 0.85, TB, 0);
                DxfText(sRight - pad, py, value, t * 0.85, TB, 2);
            }
            Rule(titleTop);

            // Drawing title (per sheet)
            DxfText(sx + pad, titleTop - t * 1.5, "DRAWING TITLE", t * 0.8, TB, 0);
            var drawingTitle = string.IsNullOrWhiteSpace(sheet.DrawingTitle) ? diagramTitle : sheet.DrawingTitle;
            DxfText(sx + pad, titleTop - t * 3.6, drawingTitle, t * 1.15, TB, 0);
            Rule(numberTop);

            // Drawing number: short box in the bottom corner
            DxfText(sx + pad, numberTop - t * 1.5, "DRAWING NUMBER", t * 0.8, TB, 0);
            DxfText(sRight - pad, sBot + t * 1.3, sheet.DrawingNumber, t * 2.0, TB, 2);
        }

        sb.AppendLine("0\nENDSEC");
        sb.AppendLine("0\nEOF");

        return sb.ToString();
    }
}
