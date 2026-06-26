using DownloaderBot.Admin.Flows;
using DownloaderBot.Audit;
using DownloaderBot.Backup;
using DownloaderBot.Configuration;
using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Handlers;
using DownloaderBot.Jobs;
using DownloaderBot.Telegram;
using DownloaderBot.Telegram.Polling;
using DownloaderBot.Telegram.RateLimiter;
using DownloaderBot.Telegram.Webhook;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;

EnvLoader.LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
var botOpts = EnvLoader.BuildBotOptions();
var webhookOpts = EnvLoader.BuildWebhookOptions();

if (string.IsNullOrWhiteSpace(botOpts.Token))
{
    Console.Error.WriteLine("BOT_TOKEN is required");
    return 1;
}

Directory.CreateDirectory(botOpts.LogDir);
Directory.CreateDirectory(Path.GetDirectoryName(botOpts.DbPath) ?? "data");
Directory.CreateDirectory(botOpts.BackupDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(botOpts.LogDir, "bot-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddSingleton(botOpts);
    builder.Services.AddSingleton(webhookOpts);

    builder.Services.AddHttpClient("tg");
    builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("tg");
        return new TelegramBotClient(botOpts.Token, http);
    });
    builder.Services.AddSingleton<RateLimitedSender>();

    builder.Services.AddDbContext<BotDbContext>(opts =>
        opts.UseSqlite($"Data Source={botOpts.DbPath}"));

    builder.Services.AddMemoryCache();

    builder.Services.AddSingleton<JobQueue>();
    builder.Services.AddHostedService<JobWorker>();

    builder.Services.AddScoped<UpdatePipeline>();
    builder.Services.AddScoped<StartHandler>();
    builder.Services.AddScoped<AdminHandler>();
    builder.Services.AddScoped<CallbackHandler>();

    builder.Services.AddScoped<ContentService>();
    builder.Services.AddScoped<LinkService>();
    builder.Services.AddScoped<MembershipService>();
    builder.Services.AddScoped<DeliveryService>();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<FsmStore>();

    builder.Services.AddHostedService<DailyBackupService>();

    if (botOpts.IsWebhookMode)
    {
        builder.Services.AddControllers();
        builder.WebHost.UseUrls(webhookOpts.Listen);
        builder.Services.AddHostedService<WebhookRegistration>();
    }
    else
    {
        builder.Services.AddHostedService<PollingService>();
        builder.WebHost.UseUrls("http://0.0.0.0:8080"); // for /health
    }

    var app = builder.Build();

    // Initialize DB + bootstrap
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        await DbInitializer.InitializeAsync(db, botOpts, default);
    }

    if (botOpts.IsWebhookMode) app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { ok = true, mode = botOpts.UpdateMode, time = DateTime.UtcNow }));

    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        try { app.Services.GetRequiredService<JobQueue>().Complete(); } catch { }
        Log.CloseAndFlush();
    });

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal startup error");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
