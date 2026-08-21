// The device library's data shape, and the diagram nodes that represent a placed device.
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace Client.Diagrams;

public class DeviceDefinition
{
    [System.Text.Json.Serialization.JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string Model { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public string Category { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("ports")]
    public List<PortDefinition> Ports { get; set; } = new();
}

public class PortDefinition
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("direction")]
    public string Direction { get; set; } = "Universal";
}

public class LineFlowNode : NodeModel
{
    public DeviceDefinition Device { get; }
    // Per-INSTANCE tag, e.g. "SW-1". Two of the same model are otherwise indistinguishable
    // on the drawing and in the cable schedule — a switch-to-switch interconnect between
    // identical models reads as a port patched back into itself without this.
    public string Label { get; set; } = "";
    // Per-block background fill. Defaults to the original navy; "transparent" is allowed.
    public string BackgroundColor { get; set; } = "#16213e";
    // Per-block outline color. Defaults to the original coral.
    public string BorderColor { get; set; } = "#e94560";
    // Per-block text color (title + port labels). Defaults to white.
    public string TextColor { get; set; } = "#ffffff";

    public LineFlowNode(Point position, DeviceDefinition device) : base(position)
    {
        Device = device;
        Title = $"{device.Manufacturer} {device.Model}";

        foreach (var port in device.Ports)
        {
            var alignment = port.Direction == "Out"
                ? PortAlignment.Right
                : port.Direction == "In"
                    ? PortAlignment.Left
                    : PortAlignment.Right;

            AddPort(new LineFlowPort(this, alignment, port));
        }
    }
}

public class LineFlowPort : PortModel
{
    public PortDefinition Definition { get; }

    public LineFlowPort(NodeModel parent, PortAlignment alignment, PortDefinition definition)
        : base(parent, alignment, null, null)
    {
        Definition = definition;
    }

    public string GetDirection() => Definition.Direction;
}
