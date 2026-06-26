using DownloaderBot.Configuration;
using DownloaderBot.Telegram.RateLimiter;
using global::Telegram.Bot;

namespace DownloaderBot.Content;

public sealed class DeliveryService
{
    readonly RateLimitedSender _sender;
    readonly BotOptions _opts;

    public DeliveryService(RateLimitedSender sender, BotOptions opts)
    {
        _sender = sender;
        _opts = opts;
    }

    public Task<global::Telegram.Bot.Types.MessageId> CopyFromStorageAsync(long toChatId, int storageMsgId, CancellationToken ct)
        => _sender.ExecuteAsync(toChatId,
            (c, t) => c.CopyMessage(toChatId, _opts.StorageChannelId, storageMsgId, cancellationToken: t), ct);

    public Task<global::Telegram.Bot.Types.Message> CopyToStorageAsync(long fromChatId, int messageId, CancellationToken ct)
        => _sender.ExecuteAsync(_opts.StorageChannelId,
            async (c, t) =>
            {
                var msgId = await c.CopyMessage(_opts.StorageChannelId, fromChatId, messageId, cancellationToken: t);
                return new global::Telegram.Bot.Types.Message { Id = msgId.Id };
            }, ct);
}
