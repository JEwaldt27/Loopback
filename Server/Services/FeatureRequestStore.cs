using System.Text.Json;
using Server.Models;

namespace Server.Services;

/// <summary>
/// File-backed feature request list (Server/feature-requests.json), mirroring the UserStore
/// pattern used elsewhere: no database, a single lock around read-modify-write so two people
/// submitting at once can't clobber each other.
/// </summary>
public class FeatureRequestStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FeatureRequestStore(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "feature-requests.json");
    }

    /// <summary>Newest first — the list is read far more often than it's written.</summary>
    public async Task<List<FeatureRequest>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try { return (await ReadAsync()).OrderByDescending(r => r.CreatedAt).ToList(); }
        finally { _lock.Release(); }
    }

    public async Task<FeatureRequest> AddAsync(string text, string username)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await ReadAsync();
            var item = new FeatureRequest
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                Status = FeatureRequest.Statuses.Received,
                CreatedBy = username,
                CreatedAt = DateTimeOffset.UtcNow
            };
            all.Add(item);
            await WriteAsync(all);
            return item;
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> to the stored item under the lock. The callback
    /// returns an error string to abort the write (used for the permission checks, so the
    /// decision is made against the *stored* status rather than whatever the client sent).
    /// </summary>
    public async Task<(bool Ok, string Error, FeatureRequest? Item)> UpdateAsync(
        string id, Func<FeatureRequest, string?> mutate)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await ReadAsync();
            var item = all.FirstOrDefault(r => r.Id == id);
            if (item == null) return (false, "Request not found.", null);

            var error = mutate(item);
            if (error != null) return (false, error, null);

            await WriteAsync(all);
            return (true, "", item);
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await ReadAsync();
            var removed = all.RemoveAll(r => r.Id == id);
            if (removed > 0) await WriteAsync(all);
            return removed > 0;
        }
        finally { _lock.Release(); }
    }

    private async Task<List<FeatureRequest>> ReadAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<List<FeatureRequest>>(json) ?? new();
    }

    private async Task WriteAsync(List<FeatureRequest> items) =>
        await File.WriteAllTextAsync(_filePath,
            JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
}
