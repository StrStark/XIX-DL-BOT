using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using ContentEntity = DownloaderBot.Data.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DownloaderBot.Tests;

public class LinkResolutionTests
{
    static BotDbContext NewDb(string name)
    {
        var opts = new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(name).Options;
        return new BotDbContext(opts);
    }

    [Fact]
    public async Task UuidMode_ResolvesExplicitContent()
    {
        await using var db = NewDb(nameof(UuidMode_ResolvesExplicitContent));
        db.Contents.Add(new ContentEntity { Uuid = "u1", Title = "t" });
        db.Links.Add(new Link { LinkId = "L", SelectionMode = LinkSelectionMode.Uuids });
        db.LinkContents.Add(new LinkContent { LinkId = "L", ContentUuid = "u1" });
        await db.SaveChangesAsync();

        var svc = new LinkService(db);
        var r = await svc.ResolveContentUuidsAsync("L", default);
        Assert.Single(r);
        Assert.Contains("u1", r);
    }

    [Fact]
    public async Task TagMode_IsDynamic()
    {
        await using var db = NewDb(nameof(TagMode_IsDynamic));
        var tag = new Tag { Name = "campaign1" };
        db.Tags.Add(tag);
        db.Contents.Add(new ContentEntity { Uuid = "u1" });
        db.Contents.Add(new ContentEntity { Uuid = "u2" });
        await db.SaveChangesAsync();
        db.ContentTags.Add(new ContentTag { ContentUuid = "u1", TagId = tag.Id });
        db.ContentTags.Add(new ContentTag { ContentUuid = "u2", TagId = tag.Id });
        db.Links.Add(new Link { LinkId = "L", SelectionMode = LinkSelectionMode.Tags });
        db.LinkTags.Add(new LinkTag { LinkId = "L", TagId = tag.Id });
        await db.SaveChangesAsync();

        var svc = new LinkService(db);
        var r = await svc.ResolveContentUuidsAsync("L", default);
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public async Task DeletedContent_Excluded()
    {
        await using var db = NewDb(nameof(DeletedContent_Excluded));
        db.Contents.Add(new ContentEntity { Uuid = "u1", Deleted = true });
        db.Links.Add(new Link { LinkId = "L", SelectionMode = LinkSelectionMode.Uuids });
        db.LinkContents.Add(new LinkContent { LinkId = "L", ContentUuid = "u1" });
        await db.SaveChangesAsync();
        var svc = new LinkService(db);
        var r = await svc.ResolveContentUuidsAsync("L", default);
        Assert.Empty(r);
    }
}
