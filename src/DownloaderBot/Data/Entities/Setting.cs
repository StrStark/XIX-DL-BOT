namespace DownloaderBot.Data.Entities;

// Generic key/value bag for runtime-tunable settings (e.g. welcome message edited by admins).
public sealed class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public static class SettingKeys
{
    public const string WelcomeText = "welcome.text";
    // For premium emoji entities (custom_emoji). Stored as JSON array of Telegram MessageEntity rows.
    public const string WelcomeEntities = "welcome.entities";
}
