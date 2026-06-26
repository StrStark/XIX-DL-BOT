using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DownloaderBot.Content;

public sealed class ContentService
{
    readonly BotDbContext _db;
    public ContentService(BotDbContext db) => _db = db;

    public static string NewUuid() => Guid.CreateVersion7().ToString("N")[..16];

    public async Task<Data.Entities.Content> AddContentAsync(long createdBy, string title, IList<int> storageMessageIds, IList<string> tags, CancellationToken ct)
    {
        var c = new Data.Entities.Content
        {
            Uuid = NewUuid(),
            Title = string.IsNullOrWhiteSpace(title) ? "(untitled)" : title.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Contents.Add(c);
        for (int i = 0; i < storageMessageIds.Count; i++)
            _db.ContentMessages.Add(new ContentMessage { ContentUuid = c.Uuid, StorageMsgId = storageMessageIds[i], OrderIndex = i });
        foreach (var tName in tags.Select(NormalizeTag).Distinct().Where(s => s.Length > 0))
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tName, ct) ?? new Tag { Name = tName };
            if (tag.Id == 0) { _db.Tags.Add(tag); await _db.SaveChangesAsync(ct); }
            _db.ContentTags.Add(new ContentTag { ContentUuid = c.Uuid, TagId = tag.Id });
        }
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task SoftDeleteAsync(string uuid, CancellationToken ct)
    {
        var c = await _db.Contents.FirstOrDefaultAsync(x => x.Uuid == uuid, ct);
        if (c is null) return;
        c.Deleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Data.Entities.Content>> ListAsync(int take, CancellationToken ct)
        => await _db.Contents.AsNoTracking().Where(c => !c.Deleted)
            .OrderByDescending(c => c.CreatedAt).Take(take).ToListAsync(ct);

    public static string NormalizeTag(string raw) => (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_');
}
