namespace DownloaderBot.Data.Entities;

public sealed class AuditEntry
{
    public int Id { get; set; }
    public long AdminTgId { get; set; }
    public string Action { get; set; } = "";
    public string? Target { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; }
}
