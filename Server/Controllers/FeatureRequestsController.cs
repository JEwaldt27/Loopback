using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

/// <summary>
/// Feature requests.
///
/// Who can do what:
///   * anyone signed in — see every request and raise a new one (always "Received")
///   * the author       — edit their own text, but ONLY while it's still "Received";
///                        once an admin moves it to WIP the wording is frozen, so what's
///                        being worked on can't change underneath them
///   * admins           — edit any text, set any status, delete
///
/// The author check is made against the stored status inside the store's lock, not against
/// anything the client sends, so a stale page can't be used to edit an in-progress request.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeatureRequestsController : ControllerBase
{
    private readonly FeatureRequestStore _store;

    public FeatureRequestsController(FeatureRequestStore store) => _store = store;

    private string CurrentUser => User.FindFirstValue(ClaimTypes.Name) ?? "";
    private bool IsAdmin => string.Equals(User.FindFirstValue(ClaimTypes.Role), "Admin",
                                          StringComparison.OrdinalIgnoreCase);

    public record CreateRequest(string? Text);
    public record UpdateRequest(string? Text, string? Status);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _store.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req)
    {
        var text = (req?.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Describe the request first." });
        if (text.Length > 2000)
            return BadRequest(new { error = "Keep it under 2000 characters." });

        return Ok(await _store.AddAsync(text, CurrentUser));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateRequest req)
    {
        var newText = req?.Text?.Trim();
        var newStatus = req?.Status?.Trim();

        if (newText is { Length: > 2000 })
            return BadRequest(new { error = "Keep it under 2000 characters." });
        if (newStatus != null && !FeatureRequest.Statuses.IsValid(newStatus))
            return BadRequest(new { error = "Unknown status." });
        if (newStatus != null && !IsAdmin)
            return StatusCode(403, new { error = "Only an admin can change the status." });

        var (ok, error, item) = await _store.UpdateAsync(id, r =>
        {
            if (newText != null)
            {
                if (string.IsNullOrWhiteSpace(newText)) return "Describe the request first.";

                var isAuthor = string.Equals(r.CreatedBy, CurrentUser, StringComparison.OrdinalIgnoreCase);
                if (!IsAdmin)
                {
                    if (!isAuthor) return "You can only edit your own requests.";
                    if (r.Status != FeatureRequest.Statuses.Received)
                        return "This request is already being worked on — ask an admin to change it.";
                }
                r.Text = newText;
            }

            if (newStatus != null) r.Status = FeatureRequest.Statuses.Normalize(newStatus);

            r.UpdatedBy = CurrentUser;
            r.UpdatedAt = DateTimeOffset.UtcNow;
            return null;
        });

        if (!ok)
            return error == "Request not found." ? NotFound(new { error }) : StatusCode(403, new { error });
        return Ok(item);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id) =>
        await _store.DeleteAsync(id) ? Ok() : NotFound(new { error = "Request not found." });
}
