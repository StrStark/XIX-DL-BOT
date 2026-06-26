using DownloaderBot.Configuration;
using DownloaderBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using AdminEntity = DownloaderBot.Data.Entities.Admin;

namespace DownloaderBot.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(BotDbContext db, BotOptions options, CancellationToken ct)
    {
        await db.Database.EnsureCreatedAsync(ct);
        // Enable WAL once
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);

        // Bootstrap superadmin
        if (options.SuperadminTgId != 0 &&
            !await db.Admins.AnyAsync(a => a.TgId == options.SuperadminTgId, ct))
        {
            db.Admins.Add(new AdminEntity
            {
                TgId = options.SuperadminTgId,
                Role = AdminRoles.Superadmin,
                AddedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        // Seed welcome text from file if no Setting yet
        if (!await db.Settings.AnyAsync(s => s.Key == SettingKeys.WelcomeText, ct))
        {
            string text = "سلام 👋\nبه ربات XIX_DL خوش آمدید.";
            try
            {
                if (File.Exists(options.WelcomeMessagePath))
                    text = await File.ReadAllTextAsync(options.WelcomeMessagePath, ct);
            }
            catch { /* ignore */ }
            db.Settings.Add(new Setting { Key = SettingKeys.WelcomeText, Value = text, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
    }
}
