using System.Text.Json;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DownloaderBot.Admin.Flows;

public sealed class FsmStore
{
    readonly BotDbContext _db;
    public FsmStore(BotDbContext db) => _db = db;

    public async Task<FsmState?> GetAsync(long adminId, CancellationToken ct)
        => await _db.FsmStates.FirstOrDefaultAsync(s => s.AdminTgId == adminId, ct);

    public async Task SetAsync(long adminId, string flow, string step, object? data, CancellationToken ct)
    {
        var json = data is null ? "{}" : JsonSerializer.Serialize(data);
        var existing = await _db.FsmStates.FirstOrDefaultAsync(s => s.AdminTgId == adminId, ct);
        if (existing is null)
            _db.FsmStates.Add(new FsmState { AdminTgId = adminId, Flow = flow, Step = step, Data = json, UpdatedAt = DateTime.UtcNow });
        else
        {
            existing.Flow = flow; existing.Step = step; existing.Data = json; existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(long adminId, CancellationToken ct)
    {
        var existing = await _db.FsmStates.FirstOrDefaultAsync(s => s.AdminTgId == adminId, ct);
        if (existing is null) return;
        _db.FsmStates.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    public static T? DeserializeData<T>(FsmState s) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(s.Data); } catch { return null; }
    }
}
