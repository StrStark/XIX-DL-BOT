using DownloaderBot.Data;
using DownloaderBot.Data.Entities;

namespace DownloaderBot.Audit;

public sealed class AuditService
{
    readonly BotDbContext _db;
    public AuditService(BotDbContext db) => _db = db;

    public async Task LogAsync(long adminId, string action, string? target = null, string? details = null, CancellationToken ct = default)
    {
        _db.AuditLog.Add(new AuditEntry
        {
            AdminTgId = adminId,
            Action = action,
            Target = target,
            Details = details,
            At = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
