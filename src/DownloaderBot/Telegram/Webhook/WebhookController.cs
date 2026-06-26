using DownloaderBot.Configuration;
using Microsoft.AspNetCore.Mvc;
using global::Telegram.Bot.Types;

namespace DownloaderBot.Telegram.Webhook;

[ApiController]
[Route("tg")]
public sealed class WebhookController : ControllerBase
{
    readonly UpdatePipeline _pipeline;
    readonly WebhookOptions _wh;

    public WebhookController(UpdatePipeline pipeline, WebhookOptions wh)
    {
        _pipeline = pipeline;
        _wh = wh;
    }

    [HttpPost("{secret}")]
    public async Task<IActionResult> Receive([FromRoute] string secret, [FromBody] Update update, CancellationToken ct)
    {
        if (!string.Equals(secret, _wh.Secret, StringComparison.Ordinal)) return Unauthorized();

        var headerSecret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
        if (!string.Equals(headerSecret, _wh.Secret, StringComparison.Ordinal)) return Unauthorized();

        await _pipeline.HandleAsync(update, ct);
        return Ok();
    }
}
