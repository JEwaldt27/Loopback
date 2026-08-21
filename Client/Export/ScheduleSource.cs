// The data behind every schedule Loopback produces: the Cable Schedule and Device Schedule
// CSVs, the Cable Count dialog, and the tables you can place on a drawing. One source of
// truth, so a schedule printed on a sheet can't disagree with the spreadsheet.
//
// The ACTIVE sheet is read from the live diagram rather than its parked JSON: a placed table
// re-reads on every render, and parking mid-render would be a side effect in the render path.
// Inactive sheets can't have changed since they were parked, so their JSON is authoritative.
using System.Text.Json;
using Blazor.Diagrams;
using Blazor.Diagrams.Core.Models;
using Client.Diagrams;

namespace Client.Export;

/// <param name="ActiveIndex">Index into <paramref name="Sheets"/> of the sheet that is live in <paramref name="Diagram"/>.</param>
public sealed record ScheduleSource(
    BlazorDiagram Diagram,
    IReadOnlyList<Sheet> Sheets,
    int ActiveIndex,
    IReadOnlyList<CableType> CableTypes,
    string NeutralColor)
{
    public string ActiveSheetName => Sheets[ActiveIndex].Name;

    public record SchedConn(string Sheet, string Cable, string CableType, string Signal,
                             string FromTag, string FromDevice, string FromPort,
                             string ToTag, string ToDevice, string ToPort, int PortIdx);

    public record SchedDev(string Sheet, string Manufacturer, string Model, string Category);

    static string DeviceTitle(string mfr, string model) => $"{mfr} {model}".Trim();

    // Rows sort by source device then source port, so every cable off one box groups
    // together — the order a pull sheet is read in.
    static List<SchedConn> SortConns(IEnumerable<SchedConn> rows) => rows
        .OrderBy(r => r.FromDevice, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.PortIdx)
        .ToList();

    IEnumerable<SchedConn> LiveConns(string sheetName)
    {
        foreach (var l in Diagram.Links.OfType<ElbowLinkModel>())
        {
            if (l.Source.Model is not LineFlowPort sp || l.Target?.Model is not LineFlowPort tp) continue;
            if (sp.Parent is not LineFlowNode src || tp.Parent is not LineFlowNode tgt) continue;

            var spi = sp.Parent.Ports.ToList().IndexOf(sp);
            yield return new SchedConn(
                sheetName, l.LabelText ?? "", l.CableTypeName ?? "", sp.Definition.Type,
                src.Label, DeviceTitle(src.Device.Manufacturer, src.Device.Model), sp.Definition.Name,
                tgt.Label, DeviceTitle(tgt.Device.Manufacturer, tgt.Device.Model), tp.Definition.Name,
                spi);
        }
    }

    static IEnumerable<SchedConn> JsonConns(string sheetName, JsonElement root)
    {
        var nodes = new Dictionary<string, (string Label, string Title, List<PortDefinition> Ports)>();
        foreach (var n in root.GetProperty("nodes").EnumerateArray())
        {
            var mfr = n.TryGetProperty("manufacturer", out var mEl) ? mEl.GetString() ?? "" : "";
            var model = n.TryGetProperty("model", out var moEl) ? moEl.GetString() ?? "" : "";
            var label = n.TryGetProperty("label", out var lEl) ? lEl.GetString() ?? "" : "";
            var ports = n.TryGetProperty("ports", out var pEl) ? pEl.Deserialize<List<PortDefinition>>() ?? new() : new();
            nodes[n.GetProperty("id").GetString()!] = (label, DeviceTitle(mfr, model), ports);
        }

        foreach (var l in root.GetProperty("links").EnumerateArray())
        {
            if (!nodes.TryGetValue(l.GetProperty("sourceNodeId").GetString() ?? "", out var src)) continue;
            if (!nodes.TryGetValue(l.GetProperty("targetNodeId").GetString() ?? "", out var tgt)) continue;
            int spi = l.GetProperty("sourcePortIndex").GetInt32();
            int tpi = l.GetProperty("targetPortIndex").GetInt32();
            if (spi >= src.Ports.Count || tpi >= tgt.Ports.Count) continue;

            yield return new SchedConn(
                sheetName,
                l.TryGetProperty("label", out var lbEl) ? lbEl.GetString() ?? "" : "",
                l.TryGetProperty("cableType", out var ctEl) && ctEl.ValueKind == JsonValueKind.String ? ctEl.GetString() ?? "" : "",
                src.Ports[spi].Type,
                src.Label, src.Title, src.Ports[spi].Name,
                tgt.Label, tgt.Title, tgt.Ports[tpi].Name,
                spi);
        }
    }

    // allSheets: the whole document (sheets in tab order, sorted within each) vs just the
    // sheet you're on.
    public List<SchedConn> Conns(bool allSheets)
    {
        if (!allSheets) return SortConns(LiveConns(ActiveSheetName));

        var rows = new List<SchedConn>();
        for (int i = 0; i < Sheets.Count; i++)
        {
            if (i == ActiveIndex)
            {
                rows.AddRange(SortConns(LiveConns(Sheets[i].Name)));
            }
            else
            {
                using var doc = JsonDocument.Parse(Sheets[i].Json ?? Sheet.EmptyJson);
                rows.AddRange(SortConns(JsonConns(Sheets[i].Name, doc.RootElement)));
            }
        }
        return rows;
    }

    public record DeviceCountRow(int Qty, string Manufacturer, string Model, string Category);

    // One row per manufacturer+model with a quantity — a BOM, not an inventory list.
    // Sorted by category then manufacturer, like the device palette.
    public List<DeviceCountRow> DeviceCounts(bool allSheets)
    {
        var counts = new Dictionary<(string Mfr, string Model, string Cat), int>();
        foreach (var d in Devices(allSheets))
        {
            var key = (d.Manufacturer, d.Model, d.Category);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts
            .Select(kv => new DeviceCountRow(kv.Value, kv.Key.Mfr, kv.Key.Model, kv.Key.Cat))
            .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Manufacturer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    List<SchedDev> Devices(bool allSheets)
    {
        var rows = new List<SchedDev>();
        for (int i = 0; i < Sheets.Count; i++)
        {
            if (!allSheets && i != ActiveIndex) continue;

            if (i == ActiveIndex)
            {
                rows.AddRange(Diagram.Nodes.OfType<LineFlowNode>().Select(n =>
                    new SchedDev(Sheets[i].Name, n.Device.Manufacturer, n.Device.Model, n.Device.Category)));
            }
            else
            {
                using var doc = JsonDocument.Parse(Sheets[i].Json ?? Sheet.EmptyJson);
                foreach (var n in doc.RootElement.GetProperty("nodes").EnumerateArray())
                    rows.Add(new SchedDev(
                        Sheets[i].Name,
                        n.TryGetProperty("manufacturer", out var mEl) ? mEl.GetString() ?? "" : "",
                        n.TryGetProperty("model", out var moEl) ? moEl.GetString() ?? "" : "",
                        n.TryGetProperty("category", out var cEl) ? cEl.GetString() ?? "" : ""));
            }
        }
        return rows;
    }

    // Key is the cable type name; "" is the bucket for connections with no type assigned,
    // which are worth showing rather than hiding — they're usually an oversight.
    public record CableCountRow(string Key, string Name, string Color, string PartNumber,
                                 string CableColor, int Total, Dictionary<string, int> PerSheet);

    // Counts per cable type, keyed by type then sheet. Shared by the dialog and by a
    // Cable Count table placed on a sheet.
    public List<CableCountRow> CableCounts(bool allSheets)
    {
        var perSheet = new Dictionary<string, Dictionary<string, int>>();
        foreach (var c in Conns(allSheets))
        {
            var bySheet = perSheet.TryGetValue(c.CableType, out var b) ? b : perSheet[c.CableType] = new();
            bySheet[c.Sheet] = bySheet.GetValueOrDefault(c.Sheet) + 1;
        }

        return perSheet
            .Select(kv =>
            {
                var ct = CableTypes.FirstOrDefault(c => c.Name == kv.Key);
                return new CableCountRow(
                    kv.Key,
                    kv.Key == "" ? "(no cable type)" : kv.Key,
                    ct?.Color ?? NeutralColor,
                    ct?.PartNumber ?? "",
                    ct?.CableColor ?? "",
                    kv.Value.Values.Sum(),
                    kv.Value);
            })
            // Untyped last — it's a loose end, not a line item to order.
            .OrderBy(r => r.Key == "" ? 1 : 0)
            .ThenByDescending(r => r.Total)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
