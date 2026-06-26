using DownloaderBot.Telegram.RateLimiter;
using Microsoft.Extensions.Caching.Memory;
using global::Telegram.Bot;
using global::Telegram.Bot.Types.Enums;

namespace DownloaderBot.Content;

public sealed class MembershipService
{
    readonly RateLimitedSender _sender;
    readonly IMemoryCache _cache;
    readonly int _ttl;
    readonly ILogger<MembershipService> _log;

    public MembershipService(RateLimitedSender sender, IMemoryCache cache, Configuration.BotOptions o, ILogger<MembershipService> log)
    {
        _sender = sender;
        _cache = cache;
        _ttl = o.MembershipCacheTtlSeconds;
        _log = log;
    }

    string Key(long chatId, long userId) => $"member:{chatId}:{userId}";

    public void Invalidate(long userId, IEnumerable<long> chatIds)
    {
        foreach (var c in chatIds) _cache.Remove(Key(c, userId));
    }

    public async Task<bool> IsMemberAsync(long chatId, long userId, CancellationToken ct)
    {
        if (_cache.TryGetValue<bool>(Key(chatId, userId), out var cached)) return cached;
        bool ok;
        try
        {
            var member = await _sender.ExecuteAsync(null,
                (c, t) => c.GetChatMember(chatId, userId, cancellationToken: t), ct);
            ok = member.Status is ChatMemberStatus.Creator
                or ChatMemberStatus.Administrator
                or ChatMemberStatus.Member
                or ChatMemberStatus.Restricted;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "getChatMember failed for chat {C} user {U}", chatId, userId);
            ok = false;
        }
        _cache.Set(Key(chatId, userId), ok, TimeSpan.FromSeconds(_ttl));
        return ok;
    }
}
