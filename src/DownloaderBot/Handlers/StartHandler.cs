using System.Text.Json;
using DownloaderBot.Configuration;
using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using DownloaderBot.Localization;
using DownloaderBot.Telegram.RateLimiter;
using Microsoft.EntityFrameworkCore;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.ReplyMarkups;

namespace DownloaderBot.Handlers;

public sealed class StartHandler
{
    readonly BotDbContext _db;
    readonly LinkService _links;
    readonly MembershipService _membership;
    readonly DeliveryService _delivery;
    readonly RateLimitedSender _sender;
    readonly BotOptions _opts;
    readonly ILogger<StartHandler> _log;

    public StartHandler(BotDbContext db, LinkService links, MembershipService membership,
        DeliveryService delivery, RateLimitedSender sender, BotOptions opts, ILogger<StartHandler> log)
    {
        _db = db; _links = links; _membership = membership; _delivery = delivery;
        _sender = sender; _opts = opts; _log = log;
    }

    public async Task HandleAsync(Message msg, CancellationToken ct)
    {
        if (msg.From is null || msg.Text is null) return;
        var payload = ParsePayload(msg.Text);

        // Referral processing — only on first interaction (first /start), so check if user already has a referrer.
        if (payload.RefTgId.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.TgId == msg.From.Id, ct);
            if (user is not null && user.ReferrerTgId is null && payload.RefTgId.Value != msg.From.Id)
            {
                user.ReferrerTgId = payload.RefTgId.Value;
                await _db.SaveChangesAsync(ct);
            }
        }

        if (!string.IsNullOrEmpty(payload.LinkId))
        {
            await HandleLinkAsync(msg.From.Id, msg.Chat.Id, payload.LinkId, ct);
            return;
        }

        // No payload — send the welcome message (with premium emoji entities if stored)
        await SendWelcomeAsync(msg.Chat.Id, ct);
    }

    async Task SendWelcomeAsync(long chatId, CancellationToken ct)
    {
        var text = (await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKeys.WelcomeText, ct))?.Value
                   ?? Strings.FallbackWelcome;
        var entitiesJson = (await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == SettingKeys.WelcomeEntities, ct))?.Value;

        MessageEntity[]? entities = null;
        if (!string.IsNullOrWhiteSpace(entitiesJson))
        {
            try { entities = JsonSerializer.Deserialize<MessageEntity[]>(entitiesJson); }
            catch { entities = null; }
        }

        await _sender.ExecuteAsync(chatId,
            (c, t) => c.SendMessage(chatId, text, entities: entities, cancellationToken: t), ct);
    }

    async Task HandleLinkAsync(long userId, long chatId, string linkId, CancellationToken ct)
    {
        var link = await _links.GetAsync(linkId, ct);
        if (link is null || !link.Active)
        {
            await _sender.ExecuteAsync(chatId, (c, t) => c.SendMessage(chatId, Strings.LinkInvalid, cancellationToken: t), ct);
            _db.LinkViews.Add(new LinkView { LinkId = linkId, UserTgId = userId, ViewedAt = DateTime.UtcNow, Delivered = false });
            await _db.SaveChangesAsync(ct);
            return;
        }

        link.Views += 1;
        _db.LinkViews.Add(new LinkView { LinkId = linkId, UserTgId = userId, ViewedAt = DateTime.UtcNow, Delivered = false });
        await _db.SaveChangesAsync(ct);

        var required = await _links.GetRequiredChannelsAsync(linkId, ct);
        var notJoined = new List<Channel>();
        foreach (var chId in required)
        {
            if (!await _membership.IsMemberAsync(chId, userId, ct))
            {
                var ch = await _db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.ChatId == chId, ct);
                if (ch is not null) notJoined.Add(ch);
            }
        }

        if (notJoined.Count > 0)
        {
            await SendJoinKeyboardAsync(chatId, linkId, notJoined, ct);
            return;
        }

        await DeliverAsync(userId, chatId, link, ct);
    }

    public async Task SendJoinKeyboardAsync(long chatId, string linkId, IEnumerable<Channel> channels, CancellationToken ct)
    {
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var ch in channels)
            rows.Add(new[] { InlineKeyboardButton.WithUrl($"📢 {ch.Title}", string.IsNullOrWhiteSpace(ch.InviteLink) ? "https://t.me/" : ch.InviteLink) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Strings.CheckMembershipButton, $"check:{linkId}") });
        var kb = new InlineKeyboardMarkup(rows);
        await _sender.ExecuteAsync(chatId,
            (c, t) => c.SendMessage(chatId, Strings.MustJoinChannels, replyMarkup: kb, cancellationToken: t), ct);
    }

    public async Task DeliverAsync(long userId, long chatId, Link link, CancellationToken ct)
    {
        var uuids = await _links.ResolveContentUuidsAsync(link.LinkId, ct);
        if (uuids.Count == 0)
        {
            await _sender.ExecuteAsync(chatId, (c, t) => c.SendMessage(chatId, Strings.ContentEmpty, cancellationToken: t), ct);
            return;
        }

        var messages = await _db.ContentMessages.AsNoTracking()
            .Where(m => uuids.Contains(m.ContentUuid))
            .OrderBy(m => m.ContentUuid).ThenBy(m => m.OrderIndex)
            .ToListAsync(ct);

        foreach (var m in messages)
        {
            try { await _delivery.CopyFromStorageAsync(chatId, m.StorageMsgId, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "Copy failed for msg {Id}", m.StorageMsgId); }
        }

        link.Deliveries += 1;
        var lastView = await _db.LinkViews.Where(v => v.LinkId == link.LinkId && v.UserTgId == userId)
            .OrderByDescending(v => v.ViewedAt).FirstOrDefaultAsync(ct);
        if (lastView is not null) lastView.Delivered = true;

        if (!await _db.Deliveries.AnyAsync(d => d.LinkId == link.LinkId && d.UserTgId == userId, ct))
            _db.Deliveries.Add(new Delivery { LinkId = link.LinkId, UserTgId = userId, DeliveredAt = DateTime.UtcNow });

        await _db.SaveChangesAsync(ct);
    }

    public sealed record StartPayload(string? LinkId, long? RefTgId);

    public static StartPayload ParsePayload(string text)
    {
        var sp = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (sp.Length < 2) return new StartPayload(null, null);
        var p = sp[1].Trim();
        if (p.StartsWith("ref_"))
            return new StartPayload(null, long.TryParse(p[4..], out var r) ? r : null);

        var refIdx = p.IndexOf("_ref_", StringComparison.Ordinal);
        if (refIdx > 0)
        {
            var linkPart = p[..refIdx];
            var refPart = p[(refIdx + 5)..];
            return new StartPayload(linkPart, long.TryParse(refPart, out var r) ? r : null);
        }
        return new StartPayload(p, null);
    }
}
