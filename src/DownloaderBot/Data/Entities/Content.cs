namespace DownloaderBot.Data.Entities;

public sealed class Content
{
    public string Uuid { get; set; } = "";
    public string Title { get; set; } = "";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Deleted { get; set; }

    public List<ContentMessage> Messages { get; set; } = new();
    public List<ContentTag> ContentTags { get; set; } = new();
}

public sealed class ContentMessage
{
    public int Id { get; set; }
    public string ContentUuid { get; set; } = "";
    public int StorageMsgId { get; set; }
    public int OrderIndex { get; set; }
}
