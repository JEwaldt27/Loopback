using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Server.Models;

namespace Server.Services;

/// <summary>
/// File-backed user store (Server/users.json), mirroring the devices.json pattern used
/// elsewhere in this project. No database — fine for a small, self-hosted user list.
/// </summary>
public class UserStore
{
    private readonly string _filePath;
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UserStore(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "users.json");
    }

    public async Task<List<AppUser>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try { return await ReadAsync(); }
        finally { _lock.Release(); }
    }

    public async Task<bool> AnyUsersAsync()
    {
        var users = await GetAllAsync();
        return users.Count > 0;
    }

    public async Task<AppUser?> FindAsync(string username)
    {
        var users = await GetAllAsync();
        return users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<(bool Success, string Error)> CreateAsync(string username, string password, string role)
    {
        username = (username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Username and password are required.");
        if (password.Length < 8)
            return (false, "Password must be at least 8 characters.");

        await _lock.WaitAsync();
        try
        {
            var users = await ReadAsync();
            if (users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                return (false, "That username already exists.");

            var user = new AppUser { Username = username, Role = role == "Admin" ? "Admin" : "User" };
            user.PasswordHash = _hasher.HashPassword(user, password);
            users.Add(user);
            await WriteAsync(users);
            return (true, "");
        }
        finally { _lock.Release(); }
    }

    // Sets a new password for an existing user (used by self-service change and admin reset).
    // Does not verify the old password — callers that require that (self-service) check it first.
    public async Task<(bool Success, string Error)> SetPasswordAsync(string username, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "Password is required.");
        if (newPassword.Length < 8)
            return (false, "Password must be at least 8 characters.");

        await _lock.WaitAsync();
        try
        {
            var users = await ReadAsync();
            var user = users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user == null) return (false, "User not found.");

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            await WriteAsync(users);
            return (true, "");
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> DeleteAsync(string username)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAsync();
            var removed = users.RemoveAll(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) await WriteAsync(users);
            return removed > 0;
        }
        finally { _lock.Release(); }
    }

    public PasswordVerificationResult VerifyPassword(AppUser user, string password)
        => _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

    private async Task<List<AppUser>> ReadAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<List<AppUser>>(json) ?? new();
    }

    private async Task WriteAsync(List<AppUser> users)
    {
        await File.WriteAllTextAsync(_filePath,
            JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true }));
    }
}
