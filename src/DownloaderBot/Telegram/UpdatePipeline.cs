using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using DownloaderBot.Handlers;
using Microsoft.EntityFrameworkCore;
using global::Telegram.Bot.Types;
using UserEntity = DownloaderBot.Data.Entities.User;

namespace DownloaderBot.Telegram;

public sealed class UpdatePipeline
{
    readonly IServiceScopeFactory _scopes;
    readonly ILogger<UpdatePipeline> _log;

    public UpdatePipeline(IServiceScopeFactory scopes, ILogger<UpdatePipeline> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<BotDbContext>();

            var (tgId, _) = ExtractUser(update);
            if (tgId != 0)
            {
                if (await db.Users.AsNoTracking().AnyAsync(u => u.TgId == tgId && u.Banned, ct))
                    return; // banned: silently drop
                await UserUpsert.UpsertAsync(db, update, ct);
            }

            // Route
            if (update.Message is { } m && m.Text is { } txt && txt.StartsWith("/start"))
            {
                await sp.GetRequiredService<StartHandler>().HandleAsync(m, ct);
                return;
            }
            if (update.Message is { } m2 && m2.Text == "/admin")
            {
                await sp.GetRequiredService<AdminHandler>().HandleCommandAsync(m2, ct);
                return;
            }
            if (update.CallbackQuery is { } cb)
            {
                await sp.GetRequiredService<CallbackHandler>().HandleAsync(cb, ct);
                return;
            }
            if (update.Message is { } m3 && m3.From is { } from)
            {
                // Could be an admin in the middle of an FSM flow OR an ignored user message.
                if (await db.Admins.AsNoTracking().AnyAsync(a => a.TgId == from.Id, ct))
                {
                    await sp.GetRequiredService<AdminHandler>().HandleMessageAsync(m3, ct);
                    return;
                }
            }
            // Otherwise ignore.
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pipeline error on update {Id}", update.Id);
        }
    }

    static (long tgId, string? username) ExtractUser(Update u)
    {
        var from = u.Message?.From ?? u.CallbackQuery?.From ?? u.EditedMessage?.From;
        return from is null ? (0, null) : (from.Id, from.Username);
    }
}

internal static class UserUpsert
{
    public static async Task UpsertAsync(BotDbContext db, Update update, CancellationToken ct)
    {
        var from = update.Message?.From ?? update.CallbackQuery?.From ?? update.EditedMessage?.From;
        if (from is null) return;
        var now = DateTime.UtcNow;
        var existing = await db.Users.FirstOrDefaultAsync(u => u.TgId == from.Id, ct);
        if (existing is null)
        {
            db.Users.Add(new UserEntity
            {
                TgId = from.Id,
                Username = from.Username,
                FirstName = from.FirstName,
                LastName = from.LastName,
                FirstSeen = now,
                LastSeen = now,
            });
        }
        else
        {
            existing.Username = from.Username;
            existing.FirstName = from.FirstName;
            existing.LastName = from.LastName;
            existing.LastSeen = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
