using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

/// <summary>
/// The images printed in the title block: the company logo at the top of the strip, and an
/// engineer's stamp/seal in the STAMP box. Server-side and shared — one set for every diagram
/// and every user, uploaded once. The alternative (storing them per diagram) would mean
/// re-uploading for each project and carrying a base64 copy inside every .lf file.
///
/// Stored as data URIs in text files rather than raw bytes, because that is exactly the form
/// jsPDF's addImage wants — it avoids juggling content types on the way back out. SVGs are
/// rasterized in the browser before they get here, since addImage can't take vector input.
/// Mirrors the devices.json pattern: no database, fine for a small self-hosted install.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogoController : ControllerBase
{
    // Roughly 3 MB of base64, i.e. ~2.2 MB of image — far more than a title block needs.
    private const int MaxLength = 3_000_000;

    private readonly string _contentRoot;

    public LogoController(IWebHostEnvironment env)
    {
        _contentRoot = env.ContentRootPath;
    }

    public record LogoRequest(string? DataUri);

    // Slots are a fixed whitelist, never a caller-supplied path fragment — the value lands
    // in a file name. The company logo keeps the original logo.txt so existing installs
    // don't lose theirs on upgrade.
    private string? PathForSlot(string? slot) => (slot ?? "").ToLowerInvariant() switch
    {
        "" or "logo" => Path.Combine(_contentRoot, "logo.txt"),
        "stamp" => Path.Combine(_contentRoot, "logo-stamp.txt"),
        _ => null
    };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? slot)
    {
        var path = PathForSlot(slot);
        if (path == null) return BadRequest(new { error = "Unknown image slot." });
        if (!System.IO.File.Exists(path)) return NoContent();
        var uri = await System.IO.File.ReadAllTextAsync(path);
        return string.IsNullOrWhiteSpace(uri) ? NoContent() : Ok(new { dataUri = uri });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LogoRequest req, [FromQuery] string? slot)
    {
        var path = PathForSlot(slot);
        if (path == null) return BadRequest(new { error = "Unknown image slot." });

        var uri = req?.DataUri ?? "";
        if (!uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "That file doesn't look like an image." });
        if (uri.Length > MaxLength)
            return BadRequest(new { error = "Image is too large — keep it under about 2 MB." });

        await System.IO.File.WriteAllTextAsync(path, uri);
        return Ok();
    }

    [HttpDelete]
    public IActionResult Delete([FromQuery] string? slot)
    {
        var path = PathForSlot(slot);
        if (path == null) return BadRequest(new { error = "Unknown image slot." });
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        return Ok();
    }
}
