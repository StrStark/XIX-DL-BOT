using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using DownloaderBot.Localization;
using DownloaderBot.Telegram.RateLimiter;
using Microsoft.EntityFrameworkCore;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;

namespace DownloaderBot.Handlers;

public sealed class CallbackHandler
{
    readonly BotDbContext _db;
    readonly LinkService _links;
    readonly MembershipService _membership;
    readonly StartHandler _start;
    readonly RateLimitedSender _sender;
    readonly AdminHandler _admin;
    readonly ILogger<CallbackHandler> _log;

    public CallbackHandler(BotDbContext db, LinkService links, MembershipService membership,
        StartHandler start, RateLimitedSender sender, AdminHandler admin, ILogger<CallbackHandler> log)
    {
        _db = db; _links = links; _membership = membership; _start = start;
        _sender = sender; _admin = admin; _log = log;
    }

    public async Task HandleAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data is null || cb.From is null) return;

        // Admin callbacks are prefixed with 'a:'
        if (cb.Data.StartsWith("a:"))
        {
            await _admin.HandleCallbackAsync(cb, ct);
            return;
        }

        if (cb.Data.StartsWith("check:"))
        {
            await HandleMembershipCheckAsync(cb, ct);
            return;
        }

        try { await _sender.ExecuteAsync(null, (c, t) => c.AnswerCallbackQuery(cb.Id, cancellationToken: t), ct); } catch { }
    }

    async Task HandleMembershipCheckAsync(CallbackQuery cb, CancellationToken ct)
    {
        var linkId = cb.Data!["check:".Length..];
        var link = await _links.GetAsync(linkId, ct);
        if (link is null)
        {
            await Answer(cb, Strings.LinkInvalid, ct);
            return;
        }
        var required = await _links.GetRequiredChannelsAsync(linkId, ct);

        // Force-bypass cache for this check
        _membership.Invalidate(cb.From.Id, required);

        var notJoined = new List<Channel>();
        foreach (var chId in required)
            if (!await _membership.IsMemberAsync(chId, cb.From.Id, ct))
            {
                var ch = await _db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.ChatId == chId, ct);
                if (ch is not null) notJoined.Add(ch);
            }

        if (notJoined.Count > 0)
        {
            await Answer(cb, Strings.NotJoinedYet, ct);
            return;
        }
        await Answer(cb, Strings.DeliverySuccess, ct);
        await _start.DeliverAsync(cb.From.Id, cb.Message!.Chat.Id, link, ct);
    }

    Task Answer(CallbackQuery cb, string text, CancellationToken ct)
        => _sender.ExecuteAsync(null,
            (c, t) => c.AnswerCallbackQuery(cb.Id, text, showAlert: false, cancellationToken: t), ct);
}
