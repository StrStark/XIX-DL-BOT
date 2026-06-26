namespace DownloaderBot.Data.Entities;

public sealed class Channel
{
    public long ChatId { get; set; }
    public string Title { get; set; } = "";
    public string InviteLink { get; set; } = "";
    public long AddedBy { get; set; }
    public DateTime AddedAt { get; set; }
    public bool Active { get; set; } = true;
}
