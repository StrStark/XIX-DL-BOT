using DownloaderBot.Configuration;
using global::Telegram.Bot;
using global::Telegram.Bot.Exceptions;

namespace DownloaderBot.Telegram.RateLimiter;

// Lightweight helper that gates outbound Telegram calls behind global + per-chat limiters
// and applies retry with exponential backoff on 5xx / honor retry_after on 429.
public sealed class RateLimitedSender
{
    readonly TokenBucketLimiter _global;
    readonly PerChatLimiter _perChat;
    readonly ITelegramBotClient _client;
    readonly ILogger<RateLimitedSender> _log;

    public RateLimitedSender(BotOptions o, ITelegramBotClient client, ILogger<RateLimitedSender> log)
    {
        _global = new TokenBucketLimiter(o.TgRateGlobalPerSec);
        _perChat = new PerChatLimiter(o.TgRatePerChatPerSec);
        _client = client;
        _log = log;
    }

    public ITelegramBotClient Client => _client;

    public async Task<T> ExecuteAsync<T>(long? chatId, Func<ITelegramBotClient, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        int attempt = 0;
        TimeSpan delay = TimeSpan.FromSeconds(1);
        while (true)
        {
            await _global.WaitAsync(ct);
            if (chatId.HasValue) await _perChat.WaitAsync(chatId.Value, ct);
            try
            {
                return await action(_client, ct);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 429)
            {
                var retry = ex.Parameters?.RetryAfter ?? 1;
                _log.LogWarning("429 from Telegram, retry_after={s}s", retry);
                await Task.Delay(TimeSpan.FromSeconds(retry + 1), ct);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode >= 500 && attempt < 5)
            {
                _log.LogWarning("5xx from Telegram ({c}): {m}", ex.ErrorCode, ex.Message);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
                attempt++;
            }
        }
    }

    public Task ExecuteAsync(long? chatId, Func<ITelegramBotClient, CancellationToken, Task> action, CancellationToken ct)
        => ExecuteAsync<bool>(chatId, async (c, t) => { await action(c, t); return true; }, ct);
}
