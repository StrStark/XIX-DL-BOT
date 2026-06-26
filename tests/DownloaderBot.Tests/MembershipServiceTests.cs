using DownloaderBot.Handlers;
using Xunit;

namespace DownloaderBot.Tests;

public class StartPayloadParsingTests
{
    [Fact]
    public void Parse_LinkOnly()
    {
        var p = StartHandler.ParsePayload("/start abc12345");
        Assert.Equal("abc12345", p.LinkId);
        Assert.Null(p.RefTgId);
    }

    [Fact]
    public void Parse_RefOnly()
    {
        var p = StartHandler.ParsePayload("/start ref_4242");
        Assert.Null(p.LinkId);
        Assert.Equal(4242, p.RefTgId);
    }

    [Fact]
    public void Parse_Combined()
    {
        var p = StartHandler.ParsePayload("/start abc12345_ref_99");
        Assert.Equal("abc12345", p.LinkId);
        Assert.Equal(99, p.RefTgId);
    }

    [Fact]
    public void Parse_NoPayload()
    {
        var p = StartHandler.ParsePayload("/start");
        Assert.Null(p.LinkId);
        Assert.Null(p.RefTgId);
    }
}
