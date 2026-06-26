using DownloaderBot.Configuration;
using DownloaderBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DownloaderBot.Backup;

public sealed class DailyBackupService : BackgroundService
{
    readonly BotOptions _opts;
    readonly IServiceScopeFactory _scopes;
    readonly ILogger<DailyBackupService> _log;

    public DailyBackupService(BotOptions opts, IServiceScopeFactory scopes, ILogger<DailyBackupService> log)
    {
        _opts = opts; _scopes = scopes; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_opts.BackupDir);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        // Initial delay so backups don't run right at startup.
        try { await Task.Delay(TimeSpan.FromMinutes(5), ct); } catch { return; }

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                var path = Path.Combine(_opts.BackupDir, $"bot-{DateTime.UtcNow:yyyyMMddHHmm}.db");
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
                await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{path.Replace("'", "''")}';", ct);

                // Trim old backups
                var files = Directory.EnumerateFiles(_opts.BackupDir, "bot-*.db")
                    .Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTimeUtc).ToList();
                foreach (var f in files.Skip(_opts.BackupKeep))
                    try { f.Delete(); } catch { }
                _log.LogInformation("Backup written: {Path}", path);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Backup failed"); }
        }
    }
}
