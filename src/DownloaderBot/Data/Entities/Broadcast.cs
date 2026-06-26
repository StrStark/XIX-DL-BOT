namespace DownloaderBot.Data.Entities;

public static class BroadcastStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Done = "done";
    public const string Cancelled = "cancelled";
}

public sealed class Broadcast
{
    public int Id { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Text { get; set; } = "";
    public int? StorageMsgId { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public string Status { get; set; } = BroadcastStatus.Pending;
}
