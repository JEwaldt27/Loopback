using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using System.Security.Claims;

namespace Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserStore _store;

    public AuthController(UserStore store)
    {
        _store = store;
    }

    public record LoginRequest(string Username, string Password);

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var hasUsers = await _store.AnyUsersAsync();
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(new
        {
            hasUsers,
            isAuthenticated,
            username = isAuthenticated ? User.Identity!.Name : null,
            role = isAuthenticated ? User.FindFirstValue(ClaimTypes.Role) : null
        });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] LoginRequest req)
    {
        if (await _store.AnyUsersAsync())
            return BadRequest(new { error = "Setup has already been completed." });

        var (success, error) = await _store.CreateAsync(req.Username, req.Password, "Admin");
        if (!success) return BadRequest(new { error });

        await SignIn(req.Username.Trim(), "Admin");
        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _store.FindAsync(req.Username ?? "");
        if (user == null) return Unauthorized(new { error = "Invalid username or password." });

        var result = _store.VerifyPassword(user, req.Password ?? "");
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid username or password." });

        await SignIn(user.Username, user.Role);
        return Ok();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    // Any signed-in user can change their OWN password by proving the current one.
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _store.FindAsync(username);
        if (user == null) return Unauthorized();

        if (_store.VerifyPassword(user, req.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "Current password is incorrect." });

        var (success, error) = await _store.SetPasswordAsync(username, req.NewPassword ?? "");
        if (!success) return BadRequest(new { error });
        return Ok();
    }

    private async Task SignIn(string username, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}
