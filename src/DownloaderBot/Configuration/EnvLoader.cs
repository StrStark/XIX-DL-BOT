namespace DownloaderBot.Configuration;

public static class EnvLoader
{
    public static void LoadDotEnv(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (val.Length >= 2 && val[0] == '"' && val[^1] == '"') val = val[1..^1];
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, val);
        }
    }

    public static BotOptions BuildBotOptions()
    {
        var o = new BotOptions
        {
            Token = Env("BOT_TOKEN") ?? "",
            Username = (Env("BOT_USERNAME") ?? "").TrimStart('@'),
            SuperadminTgId = long.TryParse(Env("BOT_SUPERADMIN_TG_ID"), out var sid) ? sid : 0,
            StorageChannelId = long.TryParse(Env("BOT_STORAGE_CHANNEL_ID"), out var cid) ? cid : 0,
            UpdateMode = Env("BOT_UPDATE_MODE") ?? "polling",
            WelcomeMessagePath = Env("BOT_WELCOME_MESSAGE_PATH") ?? "welcome.txt",
            DbPath = Env("BOT_DB_PATH") ?? "data/bot.db",
            BackupDir = Env("BOT_BACKUP_DIR") ?? "backups",
            LogDir = Env("BOT_LOG_DIR") ?? "logs",
            MembershipCacheTtlSeconds = IntEnv("BOT_MEMBERSHIP_CACHE_TTL_SECONDS", 300),
            JobQueueCapacity = IntEnv("BOT_JOB_QUEUE_CAPACITY", 10000),
            TgRateGlobalPerSec = IntEnv("BOT_TG_RATE_GLOBAL_PER_SEC", 28),
            TgRatePerChatPerSec = IntEnv("BOT_TG_RATE_PER_CHAT_PER_SEC", 1),
            BackupKeep = IntEnv("BOT_BACKUP_KEEP", 14)
        };
        return o;
    }

    public static WebhookOptions BuildWebhookOptions() => new()
    {
        Url = Env("BOT_WEBHOOK_URL") ?? "",
        Secret = Env("BOT_WEBHOOK_SECRET") ?? "",
        Listen = Env("BOT_WEBHOOK_LISTEN") ?? "http://0.0.0.0:8080"
    };

    static string? Env(string k) => Environment.GetEnvironmentVariable(k);
    static int IntEnv(string k, int d) => int.TryParse(Env(k), out var v) ? v : d;
}
