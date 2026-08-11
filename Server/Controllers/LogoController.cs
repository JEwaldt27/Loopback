using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

/// <summary>
/// The company logo printed in the title block. Server-side and shared: one logo for every
/// diagram and every user, uploaded once — the alternative (storing it per diagram) would
/// mean re-uploading for each project and carrying a base64 copy inside every .lf file.
///
/// Stored as a data URI in a text file rather than raw bytes, because that is exactly the
/// form jsPDF's addImage wants — it avoids juggling content types on the way back out.
/// Mirrors the devices.json pattern: no database, fine for a small self-hosted install.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogoController : ControllerBase
{
    // Roughly 3 MB of base64, i.e. ~2.2 MB of image — far more than a title block needs.
    private const int MaxLength = 3_000_000;

    private readonly string _filePath;

    public LogoController(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "logo.txt");
    }

    public record LogoRequest(string? DataUri);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!System.IO.File.Exists(_filePath)) return NoContent();
        var uri = await System.IO.File.ReadAllTextAsync(_filePath);
        return string.IsNullOrWhiteSpace(uri) ? NoContent() : Ok(new { dataUri = uri });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LogoRequest req)
    {
        var uri = req?.DataUri ?? "";
        if (!uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "That file doesn't look like an image." });
        if (uri.Length > MaxLength)
            return BadRequest(new { error = "Image is too large — keep it under about 2 MB." });

        await System.IO.File.WriteAllTextAsync(_filePath, uri);
        return Ok();
    }

    [HttpDelete]
    public IActionResult Delete()
    {
        if (System.IO.File.Exists(_filePath)) System.IO.File.Delete(_filePath);
        return Ok();
    }
}
