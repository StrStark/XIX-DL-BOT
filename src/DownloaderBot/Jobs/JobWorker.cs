using DownloaderBot.Configuration;
using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Telegram.RateLimiter;
using Microsoft.EntityFrameworkCore;
using global::Telegram.Bot;
using global::Telegram.Bot.Exceptions;

namespace DownloaderBot.Jobs;

public sealed class JobWorker : BackgroundService
{
    readonly JobQueue _queue;
    readonly IServiceScopeFactory _scopes;
    readonly ILogger<JobWorker> _log;

    public JobWorker(JobQueue queue, IServiceScopeFactory scopes, ILogger<JobWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                switch (job)
                {
                    case DeliverContentJob d:
                        await sp.GetRequiredService<DeliveryService>().CopyFromStorageAsync(d.ChatId, d.StorageMsgId, ct);
                        break;
                    case BroadcastJob b:
                        await ExecuteBroadcastAsync(sp, b, ct);
                        break;
                    case AuditLogJob a:
                        await sp.GetRequiredService<Audit.AuditService>().LogAsync(a.AdminTgId, a.Action, a.Target, a.Details, ct);
                        break;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Job execution failed"); }
        }
    }

    async Task ExecuteBroadcastAsync(IServiceProvider sp, BroadcastJob b, CancellationToken ct)
    {
        var delivery = sp.GetRequiredService<DeliveryService>();
        var db = sp.GetRequiredService<BotDbContext>();
        bool ok;
        try
        {
            await delivery.CopyFromStorageAsync(b.UserTgId, b.StorageMsgId, ct);
            ok = true;
        }
        catch (ApiRequestException ex)
        {
            _log.LogInformation("Broadcast to {U} failed: {M}", b.UserTgId, ex.Message);
            ok = false;
        }
        var bc = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == b.BroadcastId, ct);
        if (bc is not null)
        {
            if (ok) bc.SentCount += 1; else bc.FailedCount += 1;
            await db.SaveChangesAsync(ct);
        }
    }
}
