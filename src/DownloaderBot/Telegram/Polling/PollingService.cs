using global::Telegram.Bot;
using global::Telegram.Bot.Polling;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;

namespace DownloaderBot.Telegram.Polling;

public sealed class PollingService : BackgroundService
{
    readonly ITelegramBotClient _client;
    readonly UpdatePipeline _pipeline;
    readonly ILogger<PollingService> _log;

    public PollingService(ITelegramBotClient client, UpdatePipeline pipeline, ILogger<PollingService> log)
    {
        _client = client;
        _pipeline = pipeline;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _client.GetMe(stoppingToken);
        _log.LogInformation("Bot started (polling) as @{u} id={id}", me.Username, me.Id);

        var opts = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.EditedMessage,
                UpdateType.CallbackQuery,
                UpdateType.ChannelPost,
                UpdateType.MyChatMember
            },
            DropPendingUpdates = false
        };

        await _client.ReceiveAsync(
            updateHandler: async (_, update, ct) => await _pipeline.HandleAsync(update, ct),
            errorHandler: (_, ex, _) =>
            {
                _log.LogError(ex, "Polling error");
                return Task.CompletedTask;
            },
            receiverOptions: opts,
            cancellationToken: stoppingToken);
    }
}
