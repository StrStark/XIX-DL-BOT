using DownloaderBot.Admin.Flows;
using DownloaderBot.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DownloaderBot.Tests;

public class FsmFlowTests
{
    static BotDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task SetGetClear_RoundTrip()
    {
        await using var db = NewDb(nameof(SetGetClear_RoundTrip));
        var s = new FsmStore(db);
        await s.SetAsync(123, Flows.NewLink, "mode", new NewLinkData { Mode = "tags" }, default);
        var got = await s.GetAsync(123, default);
        Assert.NotNull(got);
        Assert.Equal("newlink", got!.Flow);
        var data = FsmStore.DeserializeData<NewLinkData>(got);
        Assert.Equal("tags", data!.Mode);
        await s.ClearAsync(123, default);
        Assert.Null(await s.GetAsync(123, default));
    }
}
