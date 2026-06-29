using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly string _filePath;

    public DevicesController(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "devices.json");
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!System.IO.File.Exists(_filePath))
        {
            var defaults = """
            [
              {
                "manufacturer": "Crestron",
                "model": "DM-NVX-350",
                "category": "AV over IP",
                "ports": [
                  { "name": "USB", "type": "USB", "direction": "In" },
                  { "name": "USB", "type": "USB", "direction": "Out" },
                  { "name": "Network 1", "type": "Network", "direction": "Universal" },
                  { "name": "Network 2", "type": "Network", "direction": "Universal" },
                  { "name": "HDMI Out", "type": "HDMI", "direction": "Out" },
                  { "name": "HDMI 1 In", "type": "HDMI", "direction": "In" },
                  { "name": "HDMI 2 In", "type": "HDMI", "direction": "In" },
                  { "name": "Audio I/O", "type": "Audio", "direction": "Universal" }
                ]
              }
            ]
            """;
            await System.IO.File.WriteAllTextAsync(_filePath, defaults);
        }

        var json = await System.IO.File.ReadAllTextAsync(_filePath);
        return Content(json, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] DeviceDefinition device)
    {
        var json = System.IO.File.Exists(_filePath)
            ? await System.IO.File.ReadAllTextAsync(_filePath)
            : "[]";

        var devices = System.Text.Json.JsonSerializer.Deserialize<List<DeviceDefinition>>(json)
            ?? new List<DeviceDefinition>();

        devices.Add(device);

        await System.IO.File.WriteAllTextAsync(_filePath,
            System.Text.Json.JsonSerializer.Serialize(devices,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        return Ok(device);
    }
}

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