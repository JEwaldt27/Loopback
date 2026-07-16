using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserStore _store;

    public UsersController(UserStore store)
    {
        _store = store;
    }

    public record CreateUserRequest(string Username, string Password, string Role);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _store.GetAllAsync();
        return Ok(users.Select(u => new { u.Username, u.Role }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var role = req.Role == "Admin" ? "Admin" : "User";
        var (success, error) = await _store.CreateAsync(req.Username, req.Password, role);
        if (!success) return BadRequest(new { error });
        return Ok();
    }

    public record ResetPasswordRequest(string NewPassword);

    // Admin resets another user's password without needing the old one (for forgotten passwords).
    [HttpPut("{username}/password")]
    public async Task<IActionResult> ResetPassword(string username, [FromBody] ResetPasswordRequest req)
    {
        var (success, error) = await _store.SetPasswordAsync(username, req.NewPassword ?? "");
        if (!success) return BadRequest(new { error });
        return Ok();
    }

    [HttpDelete("{username}")]
    public async Task<IActionResult> Delete(string username)
    {
        var currentUsername = User.Identity?.Name;
        if (string.Equals(currentUsername, username, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "You can't delete your own account while logged in." });

        var users = await _store.GetAllAsync();
        var target = users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (target == null) return NotFound();

        if (target.Role == "Admin" && users.Count(u => u.Role == "Admin") <= 1)
            return BadRequest(new { error = "Can't delete the last remaining admin account." });

        var removed = await _store.DeleteAsync(username);
        return removed ? Ok() : NotFound();
    }
}
