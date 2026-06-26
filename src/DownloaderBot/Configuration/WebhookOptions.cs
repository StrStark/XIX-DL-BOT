namespace DownloaderBot.Configuration;

public sealed class WebhookOptions
{
    public string Url { get; set; } = "";
    public string Secret { get; set; } = "";
    public string Listen { get; set; } = "http://0.0.0.0:8080";
}
