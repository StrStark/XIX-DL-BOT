using DownloaderBot.Localization;
using global::Telegram.Bot.Types.ReplyMarkups;

namespace DownloaderBot.Admin;

public static class AdminPanel
{
    public static InlineKeyboardMarkup MainMenu(bool isSuperadmin) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminContent, "a:content"),
                    InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminLinks,   "a:links") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminChannels, "a:channels"),
                    InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminUsers, "a:users") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminBroadcast, "a:broadcast"),
                    InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminStats, "a:stats") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminAudit, "a:audit"),
                    InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminSettings, "a:settings") },
            isSuperadmin
                ? new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_AdminAdmins, "a:admins") }
                : Array.Empty<InlineKeyboardButton>()
        });

    public static InlineKeyboardMarkup ContentMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ افزودن محتوا", "a:content:add"),
                InlineKeyboardButton.WithCallbackData("📜 لیست", "a:content:list") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 حذف", "a:content:del"),
                InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup LinksMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ لینک جدید", "a:links:new"),
                InlineKeyboardButton.WithCallbackData("📜 لیست", "a:links:list") },
        new[] { InlineKeyboardButton.WithCallbackData("🚫 غیرفعال", "a:links:deact"),
                InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup ChannelsMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ افزودن کانال", "a:channels:add"),
                InlineKeyboardButton.WithCallbackData("📜 لیست", "a:channels:list") },
        new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup AdminsMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ افزودن مدیر", "a:admins:add"),
                InlineKeyboardButton.WithCallbackData("📜 لیست", "a:admins:list") },
        new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup UsersMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔎 جستجو", "a:users:search"),
                InlineKeyboardButton.WithCallbackData("⛔️ بن", "a:users:ban") },
        new[] { InlineKeyboardButton.WithCallbackData("✅ آنبن", "a:users:unban"),
                InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup SettingsMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("✏️ ویرایش پیام خوش‌آمدگویی", "a:settings:welcome") },
        new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Back, "a:menu") }
    });

    public static InlineKeyboardMarkup CancelOnly() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") }
    });
}
