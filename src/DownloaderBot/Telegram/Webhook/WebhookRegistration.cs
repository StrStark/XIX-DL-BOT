using DownloaderBot.Configuration;
using global::Telegram.Bot;

namespace DownloaderBot.Telegram.Webhook;

public sealed class WebhookRegistration : IHostedService
{
    readonly ITelegramBotClient _client;
    readonly WebhookOptions _wh;
    readonly ILogger<WebhookRegistration> _log;

    public WebhookRegistration(ITelegramBotClient client, WebhookOptions wh, ILogger<WebhookRegistration> log)
    {
        _client = client;
        _wh = wh;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_wh.Url) || string.IsNullOrWhiteSpace(_wh.Secret))
        {
            _log.LogWarning("Webhook URL or Secret missing; webhook NOT registered.");
            return;
        }
        await _client.SetWebhook(url: _wh.Url, secretToken: _wh.Secret, cancellationToken: ct);
        _log.LogInformation("Webhook registered at {Url}", _wh.Url);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        try { await _client.DeleteWebhook(cancellationToken: ct); }
        catch (Exception ex) { _log.LogWarning(ex, "Failed to delete webhook"); }
    }
}
