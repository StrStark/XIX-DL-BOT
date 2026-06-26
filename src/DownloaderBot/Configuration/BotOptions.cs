namespace DownloaderBot.Configuration;

public sealed class BotOptions
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public long SuperadminTgId { get; set; }
    public long StorageChannelId { get; set; }

    public string UpdateMode { get; set; } = "polling"; // polling | webhook
    public string WelcomeMessagePath { get; set; } = "welcome.txt";

    public string DbPath { get; set; } = "data/bot.db";
    public string BackupDir { get; set; } = "backups";
    public string LogDir { get; set; } = "logs";

    public int MembershipCacheTtlSeconds { get; set; } = 300;
    public int JobQueueCapacity { get; set; } = 10000;
    public int TgRateGlobalPerSec { get; set; } = 28;
    public int TgRatePerChatPerSec { get; set; } = 1;
    public int BackupKeep { get; set; } = 14;

    public bool IsWebhookMode => string.Equals(UpdateMode, "webhook", StringComparison.OrdinalIgnoreCase);
}
