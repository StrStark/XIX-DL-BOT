using System.Security.Cryptography;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DownloaderBot.Content;

public sealed class LinkService
{
    readonly BotDbContext _db;
    public LinkService(BotDbContext db) => _db = db;

    const string Base62 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string NewLinkId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var arr = new char[8];
        for (int i = 0; i < 8; i++) arr[i] = Base62[bytes[i] % 62];
        return new string(arr);
    }

    public async Task<Link> CreateAsync(long createdBy, string mode, string? name,
        IList<string> tags, IList<string> uuids, IList<long> channelIds, CancellationToken ct)
    {
        var link = new Link
        {
            LinkId = NewLinkId(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            SelectionMode = LinkSelectionMode.IsValid(mode) ? mode : LinkSelectionMode.Uuids,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Active = true
        };
        _db.Links.Add(link);

        foreach (var tName in tags.Select(ContentService.NormalizeTag).Distinct().Where(s => s.Length > 0))
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tName, ct) ?? new Tag { Name = tName };
            if (tag.Id == 0) { _db.Tags.Add(tag); await _db.SaveChangesAsync(ct); }
            _db.LinkTags.Add(new LinkTag { LinkId = link.LinkId, TagId = tag.Id });
        }
        foreach (var u in uuids.Distinct())
            if (await _db.Contents.AnyAsync(c => c.Uuid == u && !c.Deleted, ct))
                _db.LinkContents.Add(new LinkContent { LinkId = link.LinkId, ContentUuid = u });
        foreach (var ch in channelIds.Distinct())
            if (await _db.Channels.AnyAsync(c => c.ChatId == ch && c.Active, ct))
                _db.LinkChannels.Add(new LinkChannel { LinkId = link.LinkId, ChatId = ch });

        await _db.SaveChangesAsync(ct);
        return link;
    }

    public async Task<Link?> GetAsync(string linkId, CancellationToken ct)
        => await _db.Links.FirstOrDefaultAsync(l => l.LinkId == linkId, ct);

    // Resolve content for a link at delivery time. For tag-based modes, this is dynamic — new
    // content matching tags shows up automatically. UNION semantics: a content item with ANY
    // of the link's tags qualifies.
    public async Task<List<string>> ResolveContentUuidsAsync(string linkId, CancellationToken ct)
    {
        var link = await _db.Links.AsNoTracking().FirstOrDefaultAsync(l => l.LinkId == linkId, ct);
        if (link is null) return new();

        var result = new HashSet<string>();

        if (link.SelectionMode is LinkSelectionMode.Uuids or LinkSelectionMode.Both)
        {
            var explicitUuids = await _db.LinkContents.AsNoTracking()
                .Where(lc => lc.LinkId == linkId)
                .Select(lc => lc.ContentUuid).ToListAsync(ct);
            foreach (var u in explicitUuids) result.Add(u);
        }
        if (link.SelectionMode is LinkSelectionMode.Tags or LinkSelectionMode.Both)
        {
            var tagIds = await _db.LinkTags.AsNoTracking()
                .Where(lt => lt.LinkId == linkId).Select(lt => lt.TagId).ToListAsync(ct);
            if (tagIds.Count > 0)
            {
                var uuids = await _db.ContentTags.AsNoTracking()
                    .Where(ct2 => tagIds.Contains(ct2.TagId))
                    .Select(ct2 => ct2.ContentUuid).Distinct().ToListAsync(ct);
                foreach (var u in uuids) result.Add(u);
            }
        }

        // Filter out deleted
        var liveUuids = await _db.Contents.AsNoTracking()
            .Where(c => result.Contains(c.Uuid) && !c.Deleted)
            .Select(c => c.Uuid).ToListAsync(ct);
        return liveUuids;
    }

    public async Task<List<long>> GetRequiredChannelsAsync(string linkId, CancellationToken ct)
        => await _db.LinkChannels.AsNoTracking()
            .Where(lc => lc.LinkId == linkId)
            .Select(lc => lc.ChatId).ToListAsync(ct);
}
