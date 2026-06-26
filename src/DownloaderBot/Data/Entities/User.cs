namespace DownloaderBot.Data.Entities;

public sealed class User
{
    public long TgId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public long? ReferrerTgId { get; set; }
    public bool Banned { get; set; }
}
