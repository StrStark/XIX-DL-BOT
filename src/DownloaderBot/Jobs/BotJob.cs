namespace DownloaderBot.Jobs;

public abstract record BotJob;

public sealed record DeliverContentJob(long ChatId, int StorageMsgId) : BotJob;

public sealed record BroadcastJob(int BroadcastId, long UserTgId, int StorageMsgId) : BotJob;

public sealed record AuditLogJob(long AdminTgId, string Action, string? Target, string? Details) : BotJob;
