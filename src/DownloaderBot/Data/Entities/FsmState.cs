namespace DownloaderBot.Data.Entities;

public sealed class FsmState
{
    public long AdminTgId { get; set; }
    public string Flow { get; set; } = "";
    public string Step { get; set; } = "";
    public string Data { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
