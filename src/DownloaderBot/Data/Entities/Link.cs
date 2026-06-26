namespace DownloaderBot.Data.Entities;

public static class LinkSelectionMode
{
    public const string Tags = "tags";
    public const string Uuids = "uuids";
    public const string Both = "both";
    public static bool IsValid(string m) => m is Tags or Uuids or Both;
}

public sealed class Link
{
    public string LinkId { get; set; } = "";
    public string? Name { get; set; }
    public string SelectionMode { get; set; } = LinkSelectionMode.Uuids;
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; } = true;
    public int Views { get; set; }
    public int Deliveries { get; set; }
}

public sealed class LinkContent
{
    public string LinkId { get; set; } = "";
    public string ContentUuid { get; set; } = "";
}

public sealed class LinkChannel
{
    public string LinkId { get; set; } = "";
    public long ChatId { get; set; }
}

public sealed class Delivery
{
    public int Id { get; set; }
    public string LinkId { get; set; } = "";
    public long UserTgId { get; set; }
    public DateTime DeliveredAt { get; set; }
}

public sealed class LinkView
{
    public int Id { get; set; }
    public string LinkId { get; set; } = "";
    public long UserTgId { get; set; }
    public DateTime ViewedAt { get; set; }
    public bool Delivered { get; set; }
}
