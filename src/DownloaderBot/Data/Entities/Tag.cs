namespace DownloaderBot.Data.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ContentTag
{
    public string ContentUuid { get; set; } = "";
    public int TagId { get; set; }
}

public sealed class LinkTag
{
    public string LinkId { get; set; } = "";
    public int TagId { get; set; }
}
