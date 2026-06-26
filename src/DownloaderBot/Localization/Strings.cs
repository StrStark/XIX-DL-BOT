namespace DownloaderBot.Localization;

// All user/admin facing Persian text in one place.
public static class Strings
{
    public const string FallbackWelcome = "سلام 👋\nبه ربات XIX_DL خوش آمدید.";

    public const string MustJoinChannels = "برای دریافت محتوا ابتدا در کانال‌های زیر عضو شوید:";
    public const string CheckMembershipButton = "✅ عضو شدم";
    public const string NotJoinedYet = "❗️هنوز در همه‌ی کانال‌ها عضو نشده‌اید.";
    public const string DeliverySuccess = "✅ محتوا برای شما ارسال شد.";
    public const string LinkInvalid = "❌ این لینک نامعتبر یا غیرفعال است.";
    public const string ContentEmpty = "❌ محتوایی برای ارسال یافت نشد.";
    public const string Banned = "⛔️ دسترسی شما مسدود است.";

    // Admin panel
    public const string AdminWelcome = "🎛 پنل مدیریت";
    public const string AdminUnauthorized = "⛔️ شما مدیر نیستید.";

    public const string Btn_AdminContent = "📁 محتوا";
    public const string Btn_AdminLinks = "🔗 لینک‌ها";
    public const string Btn_AdminChannels = "📢 کانال‌ها";
    public const string Btn_AdminAdmins = "👥 مدیران";
    public const string Btn_AdminBroadcast = "📣 پیام همگانی";
    public const string Btn_AdminUsers = "👤 کاربران";
    public const string Btn_AdminStats = "📊 آمار";
    public const string Btn_AdminAudit = "📜 لاگ";
    public const string Btn_AdminSettings = "⚙️ تنظیمات";
    public const string Btn_Back = "« بازگشت";
    public const string Btn_Cancel = "✖️ انصراف";

    public const string AddContent_Prompt = "محتوای موردنظر (پیام/آلبوم/فایل/متن/لینک) را به این چت ارسال کنید.";
    public const string AddContent_AskTitle = "عنوان محتوا را وارد کنید:";
    public const string AddContent_AskTags = "تگ‌ها را با کاما (,) جدا کنید (یا — برای رد کردن):";
    public const string AddContent_Done = "✅ محتوا با شناسه زیر ذخیره شد:\n";

    public const string AddChannel_Prompt = "آی‌دی عددی کانال (با - شروع می‌شود) یا یوزرنیم را بفرستید. ربات باید در کانال ادمین باشد.";
    public const string AddChannel_NotAdmin = "❌ ربات در این کانال ادمین نیست.";
    public const string AddChannel_Done = "✅ کانال ذخیره شد.";

    public const string NewLink_PickMode = "روش انتخاب محتوا برای لینک؟";
    public const string NewLink_PickModeTags = "بر اساس تگ‌ها";
    public const string NewLink_PickModeUuids = "بر اساس UUID";
    public const string NewLink_PickModeBoth = "هردو";
    public const string NewLink_AskTags = "تگ‌ها را با کاما جدا کنید:";
    public const string NewLink_AskUuids = "UUID‌ها را با کاما جدا کنید:";
    public const string NewLink_AskChannels = "کانال‌های اجباری را انتخاب کنید:";
    public const string NewLink_AskName = "یک نام دلخواه برای لینک وارد کنید (یا — برای رد):";
    public const string NewLink_Done = "✅ لینک ساخته شد:\n";

    public const string Broadcast_AskMessage = "پیام پخش همگانی را ارسال کنید (متن یا مدیا):";
    public const string Broadcast_Started = "📣 پخش آغاز شد.";

    public const string SetWelcome_Prompt =
        "متن خوش‌آمدگویی جدید را ارسال کنید. می‌توانید از ایموجی پریمیوم استفاده کنید — تمام فرمت‌بندی پیام (شامل ایموجی پریمیوم) حفظ خواهد شد.";
    public const string SetWelcome_Saved = "✅ متن خوش‌آمدگویی ذخیره شد.";

    public const string Generic_Cancelled = "انصراف داده شد.";
    public const string Generic_NotFound = "❌ یافت نشد.";
    public const string Generic_Saved = "✅ ذخیره شد.";
    public const string Generic_Deleted = "🗑 حذف شد.";
}
