using System.Text;
using System.Text.Json;
using DownloaderBot.Admin;
using DownloaderBot.Admin.Flows;
using AdminEntity = DownloaderBot.Data.Entities.Admin;
using UserEntity = DownloaderBot.Data.Entities.User;
using DownloaderBot.Audit;
using DownloaderBot.Configuration;
using DownloaderBot.Content;
using DownloaderBot.Data;
using DownloaderBot.Data.Entities;
using DownloaderBot.Jobs;
using DownloaderBot.Localization;
using DownloaderBot.Telegram.RateLimiter;
using Microsoft.EntityFrameworkCore;
using global::Telegram.Bot;
using global::Telegram.Bot.Exceptions;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using global::Telegram.Bot.Types.ReplyMarkups;

namespace DownloaderBot.Handlers;

public sealed class AdminHandler
{
    readonly BotDbContext _db;
    readonly FsmStore _fsm;
    readonly ContentService _content;
    readonly LinkService _links;
    readonly DeliveryService _delivery;
    readonly AuditService _audit;
    readonly RateLimitedSender _sender;
    readonly JobQueue _jobs;
    readonly BotOptions _opts;
    readonly ILogger<AdminHandler> _log;

    public AdminHandler(BotDbContext db, FsmStore fsm, ContentService content, LinkService links,
        DeliveryService delivery, AuditService audit, RateLimitedSender sender, JobQueue jobs,
        BotOptions opts, ILogger<AdminHandler> log)
    {
        _db = db; _fsm = fsm; _content = content; _links = links; _delivery = delivery;
        _audit = audit; _sender = sender; _jobs = jobs; _opts = opts; _log = log;
    }

    async Task<AdminEntity?> GetAdminAsync(long tgId, CancellationToken ct)
        => await _db.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.TgId == tgId, ct);

    static bool CanManage(string role) => role is AdminRoles.Superadmin or AdminRoles.ContentManager;
    static bool IsSuper(string role) => role is AdminRoles.Superadmin;

    // ===================== /admin =====================
    public async Task HandleCommandAsync(Message m, CancellationToken ct)
    {
        if (m.From is null) return;
        var adm = await GetAdminAsync(m.From.Id, ct);
        if (adm is null) { await Reply(m.Chat.Id, Strings.AdminUnauthorized, ct); return; }
        await Reply(m.Chat.Id, Strings.AdminWelcome, ct, AdminPanel.MainMenu(IsSuper(adm.Role)));
    }

    // ===================== Callbacks =====================
    public async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.From is null || cb.Data is null || cb.Message is null) return;
        var adm = await GetAdminAsync(cb.From.Id, ct);
        if (adm is null) { await Answer(cb, Strings.AdminUnauthorized, ct); return; }

        var data = cb.Data;
        var chatId = cb.Message.Chat.Id;

        if (data == "a:menu") { await Edit(cb, Strings.AdminWelcome, AdminPanel.MainMenu(IsSuper(adm.Role)), ct); return; }
        if (data == "a:cancel") { await _fsm.ClearAsync(adm.TgId, ct); await Edit(cb, Strings.Generic_Cancelled, AdminPanel.MainMenu(IsSuper(adm.Role)), ct); return; }

        if (data == "a:content")  { await Edit(cb, "📁 محتوا", AdminPanel.ContentMenu(), ct); return; }
        if (data == "a:links")    { await Edit(cb, "🔗 لینک‌ها", AdminPanel.LinksMenu(), ct); return; }
        if (data == "a:channels") { await Edit(cb, "📢 کانال‌ها", AdminPanel.ChannelsMenu(), ct); return; }
        if (data == "a:admins" && IsSuper(adm.Role)) { await Edit(cb, "👥 مدیران", AdminPanel.AdminsMenu(), ct); return; }
        if (data == "a:users")    { await Edit(cb, "👤 کاربران", AdminPanel.UsersMenu(), ct); return; }
        if (data == "a:settings") { await Edit(cb, "⚙️ تنظیمات", AdminPanel.SettingsMenu(), ct); return; }

        if (data == "a:content:add" && CanManage(adm.Role)) { await StartAddContentAsync(cb, ct); return; }
        if (data == "a:content:list") { await ListContentAsync(cb, ct); return; }
        if (data == "a:content:del" && CanManage(adm.Role)) { await StartDelContentAsync(cb, ct); return; }

        if (data == "a:channels:add" && CanManage(adm.Role)) { await StartAddChannelAsync(cb, ct); return; }
        if (data == "a:channels:list") { await ListChannelsAsync(cb, ct); return; }

        if (data == "a:links:new" && CanManage(adm.Role)) { await StartNewLinkAsync(cb, ct); return; }
        if (data == "a:links:list") { await ListLinksAsync(cb, ct); return; }
        if (data == "a:links:deact" && CanManage(adm.Role)) { await StartDeactLinkAsync(cb, ct); return; }

        if (data == "a:admins:add" && IsSuper(adm.Role)) { await StartAddAdminAsync(cb, ct); return; }
        if (data == "a:admins:list" && IsSuper(adm.Role)) { await ListAdminsAsync(cb, ct); return; }

        if (data == "a:users:search") { await StartSearchUserAsync(cb, ct); return; }
        if (data == "a:users:ban" && CanManage(adm.Role)) { await StartBanUserAsync(cb, ct); return; }
        if (data == "a:users:unban" && CanManage(adm.Role)) { await StartUnbanUserAsync(cb, ct); return; }

        if (data == "a:broadcast" && CanManage(adm.Role)) { await StartBroadcastAsync(cb, ct); return; }

        if (data == "a:stats") { await ShowStatsAsync(cb, ct); return; }
        if (data == "a:audit" && IsSuper(adm.Role)) { await ShowAuditAsync(cb, ct); return; }

        if (data == "a:settings:welcome" && CanManage(adm.Role)) { await StartSetWelcomeAsync(cb, ct); return; }

        // NewLink callbacks
        if (data.StartsWith("a:nl:mode:") && CanManage(adm.Role))
        {
            var mode = data["a:nl:mode:".Length..];
            await NewLink_PickMode(cb, mode, ct);
            return;
        }
        if (data.StartsWith("a:nl:ch:") && CanManage(adm.Role))
        {
            var rest = data["a:nl:ch:".Length..];
            await NewLink_ToggleChannel(cb, rest, ct);
            return;
        }
        if (data == "a:nl:ch:done" && CanManage(adm.Role)) { await NewLink_ChannelsDone(cb, ct); return; }
        if (data == "a:addcontent:save" && CanManage(adm.Role)) { await AddContent_Save(cb, ct); return; }

        if (data.StartsWith("a:role:") && IsSuper(adm.Role))
        {
            var role = data["a:role:".Length..];
            await AddAdmin_PickRole(cb, role, ct);
            return;
        }

        try { await _sender.ExecuteAsync(null, (c, t) => c.AnswerCallbackQuery(cb.Id, cancellationToken: t), ct); } catch { }
    }

    // ===================== Free-text dispatcher =====================
    public async Task HandleMessageAsync(Message m, CancellationToken ct)
    {
        if (m.From is null) return;
        var adm = await GetAdminAsync(m.From.Id, ct);
        if (adm is null) return;
        var st = await _fsm.GetAsync(m.From.Id, ct);
        if (st is null) return;

        switch (st.Flow)
        {
            case Flows.AddContent: await AddContent_ReceiveMessage(m, st, ct); break;
            case Flows.AddChannel: await AddChannel_ReceiveText(m, st, ct); break;
            case Flows.NewLink:    await NewLink_ReceiveText(m, st, ct); break;
            case Flows.AddAdmin:   await AddAdmin_ReceiveText(m, st, ct); break;
            case Flows.Broadcast:  await Broadcast_ReceiveMessage(m, st, ct); break;
            case Flows.SetWelcome: await SetWelcome_ReceiveMessage(m, st, ct); break;
            case Flows.DelContent: await DelContent_Receive(m, ct); break;
            case Flows.DeactLink:  await DeactLink_Receive(m, ct); break;
            case Flows.SearchUser: await SearchUser_Receive(m, ct); break;
            case Flows.BanUser:    await BanUser_Receive(m, adm, ct); break;
            case Flows.UnbanUser:  await UnbanUser_Receive(m, adm, ct); break;
        }
    }

    // ===================== ADD CONTENT =====================
    async Task StartAddContentAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.AddContent, "collect", new AddContentData(), ct);
        await Edit(cb, Strings.AddContent_Prompt, AdminPanel.CancelOnly(), ct);
    }

    async Task AddContent_ReceiveMessage(Message m, FsmState st, CancellationToken ct)
    {
        var data = FsmStore.DeserializeData<AddContentData>(st) ?? new AddContentData();

        if (st.Step == "collect")
        {
            try
            {
                var copied = await _delivery.CopyToStorageAsync(m.Chat.Id, m.Id, ct);
                data.StorageMsgIds.Add(copied.Id);
                data.MediaGroupId = m.MediaGroupId;
                data.LastMsgAt = DateTime.UtcNow;
                await _fsm.SetAsync(m.From!.Id, Flows.AddContent, "collect", data, ct);

                // For albums: wait for more parts. Provide explicit "save" button.
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData($"💾 ذخیره ({data.StorageMsgIds.Count} پیام)", "a:addcontent:save"),
                            InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") }
                });
                await Reply(m.Chat.Id, $"📥 پیام دریافت شد ({data.StorageMsgIds.Count}). در صورت آلبوم، بقیه را هم بفرستید سپس «ذخیره».", ct, kb);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Copy to storage failed");
                await Reply(m.Chat.Id, "❌ کپی به کانال ذخیره‌سازی شکست خورد. بررسی کنید که ربات در کانال ذخیره ادمین است.", ct);
            }
        }
        else if (st.Step == "title")
        {
            data.Title = m.Text;
            await _fsm.SetAsync(m.From!.Id, Flows.AddContent, "tags", data, ct);
            await Reply(m.Chat.Id, Strings.AddContent_AskTags, ct, AdminPanel.CancelOnly());
        }
        else if (st.Step == "tags")
        {
            var tags = (m.Text ?? "").Trim() is "-" or "—" ? new List<string>() :
                (m.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var c = await _content.AddContentAsync(m.From!.Id, data.Title ?? "(untitled)", data.StorageMsgIds, tags, ct);
            await _audit.LogAsync(m.From.Id, "content.add", c.Uuid, JsonSerializer.Serialize(new { c.Title, count = data.StorageMsgIds.Count, tags }), ct);
            await _fsm.ClearAsync(m.From.Id, ct);
            await Reply(m.Chat.Id, Strings.AddContent_Done + "<code>" + c.Uuid + "</code>", ct, parseMode: ParseMode.Html);
        }
    }

    async Task AddContent_Save(CallbackQuery cb, CancellationToken ct)
    {
        var st = await _fsm.GetAsync(cb.From.Id, ct);
        if (st is null || st.Flow != Flows.AddContent) { await Answer(cb, Strings.Generic_NotFound, ct); return; }
        var data = FsmStore.DeserializeData<AddContentData>(st) ?? new AddContentData();
        if (data.StorageMsgIds.Count == 0) { await Answer(cb, "هیچ پیامی دریافت نشده.", ct); return; }
        await _fsm.SetAsync(cb.From.Id, Flows.AddContent, "title", data, ct);
        await Edit(cb, Strings.AddContent_AskTitle, AdminPanel.CancelOnly(), ct);
    }

    // ===================== ADD CHANNEL =====================
    async Task StartAddChannelAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.AddChannel, "input", new AddChannelData(), ct);
        await Edit(cb, Strings.AddChannel_Prompt, AdminPanel.CancelOnly(), ct);
    }

    async Task AddChannel_ReceiveText(Message m, FsmState st, CancellationToken ct)
    {
        var input = (m.Text ?? "").Trim();
        long chatId = 0;
        try
        {
            ChatFullInfo info;
            if (input.StartsWith("@")) info = await _sender.ExecuteAsync(null, (c, t) => c.GetChat(input, t), ct);
            else if (long.TryParse(input, out var parsed)) info = await _sender.ExecuteAsync(null, (c, t) => c.GetChat(parsed, t), ct);
            else { await Reply(m.Chat.Id, "❌ ورودی نامعتبر.", ct); return; }
            chatId = info.Id;

            var bot = await _sender.ExecuteAsync(null, (c, t) => c.GetMe(t), ct);
            var member = await _sender.ExecuteAsync(null, (c, t) => c.GetChatMember(chatId, bot.Id, cancellationToken: t), ct);
            if (member.Status is not (ChatMemberStatus.Administrator or ChatMemberStatus.Creator))
            { await Reply(m.Chat.Id, Strings.AddChannel_NotAdmin, ct); return; }

            string invite = info.InviteLink ?? "";
            if (string.IsNullOrEmpty(invite))
            {
                try { invite = await _sender.ExecuteAsync(null, (c, t) => c.ExportChatInviteLink(chatId, t), ct); } catch { invite = ""; }
            }

            var existing = await _db.Channels.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
            if (existing is null)
                _db.Channels.Add(new Channel { ChatId = chatId, Title = info.Title ?? "", InviteLink = invite, AddedBy = m.From!.Id, AddedAt = DateTime.UtcNow, Active = true });
            else { existing.Title = info.Title ?? existing.Title; existing.InviteLink = invite; existing.Active = true; }
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(m.From!.Id, "channel.add", chatId.ToString(), null, ct);
            await _fsm.ClearAsync(m.From.Id, ct);
            await Reply(m.Chat.Id, Strings.AddChannel_Done, ct);
        }
        catch (ApiRequestException ex)
        {
            await Reply(m.Chat.Id, $"❌ خطا: {ex.Message}", ct);
        }
    }

    // ===================== NEW LINK =====================
    async Task StartNewLinkAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.NewLink, "mode", new NewLinkData(), ct);
        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(Strings.NewLink_PickModeTags, "a:nl:mode:tags") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.NewLink_PickModeUuids, "a:nl:mode:uuids") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.NewLink_PickModeBoth, "a:nl:mode:both") },
            new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") }
        });
        await Edit(cb, Strings.NewLink_PickMode, kb, ct);
    }

    async Task NewLink_PickMode(CallbackQuery cb, string mode, CancellationToken ct)
    {
        var st = await _fsm.GetAsync(cb.From.Id, ct);
        if (st is null || st.Flow != Flows.NewLink) return;
        var data = FsmStore.DeserializeData<NewLinkData>(st) ?? new NewLinkData();
        data.Mode = mode;
        var next = mode == LinkSelectionMode.Uuids ? "uuids" : "tags";
        await _fsm.SetAsync(cb.From.Id, Flows.NewLink, next, data, ct);
        await Edit(cb, mode == LinkSelectionMode.Uuids ? Strings.NewLink_AskUuids : Strings.NewLink_AskTags, AdminPanel.CancelOnly(), ct);
    }

    async Task NewLink_ReceiveText(Message m, FsmState st, CancellationToken ct)
    {
        var data = FsmStore.DeserializeData<NewLinkData>(st) ?? new NewLinkData();
        var input = (m.Text ?? "").Trim();
        switch (st.Step)
        {
            case "tags":
                data.Tags = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (data.Mode == LinkSelectionMode.Both)
                {
                    await _fsm.SetAsync(m.From!.Id, Flows.NewLink, "uuids", data, ct);
                    await Reply(m.Chat.Id, Strings.NewLink_AskUuids, ct, AdminPanel.CancelOnly());
                }
                else
                {
                    await _fsm.SetAsync(m.From!.Id, Flows.NewLink, "channels", data, ct);
                    await SendChannelMultiSelect(m.Chat.Id, data, ct);
                }
                break;
            case "uuids":
                data.Uuids = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                await _fsm.SetAsync(m.From!.Id, Flows.NewLink, "channels", data, ct);
                await SendChannelMultiSelect(m.Chat.Id, data, ct);
                break;
            case "name":
                data.Name = input is "-" or "—" ? null : input;
                var link = await _links.CreateAsync(m.From!.Id, data.Mode, data.Name, data.Tags, data.Uuids, data.ChannelIds, ct);
                await _audit.LogAsync(m.From.Id, "link.create", link.LinkId, JsonSerializer.Serialize(data), ct);
                await _fsm.ClearAsync(m.From.Id, ct);
                var url = $"https://t.me/{_opts.Username}?start={link.LinkId}";
                await Reply(m.Chat.Id, Strings.NewLink_Done + url, ct);
                break;
        }
    }

    async Task SendChannelMultiSelect(long chatId, NewLinkData data, CancellationToken ct)
    {
        var chans = await _db.Channels.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Title).ToListAsync(ct);
        if (chans.Count == 0)
        {
            await Reply(chatId, "❌ هیچ کانالی ثبت نشده. ابتدا کانال اضافه کنید.", ct);
            return;
        }
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var ch in chans)
        {
            var marked = data.ChannelIds.Contains(ch.ChatId) ? "✅ " : "▫️ ";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(marked + ch.Title, $"a:nl:ch:{ch.ChatId}") });
        }
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✔️ انتخاب نهایی", "a:nl:ch:done"),
                         InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") });
        await Reply(chatId, Strings.NewLink_AskChannels, ct, new InlineKeyboardMarkup(rows));
    }

    async Task NewLink_ToggleChannel(CallbackQuery cb, string idStr, CancellationToken ct)
    {
        if (idStr == "done") return;
        if (!long.TryParse(idStr, out var chId)) return;
        var st = await _fsm.GetAsync(cb.From.Id, ct);
        if (st is null) return;
        var data = FsmStore.DeserializeData<NewLinkData>(st) ?? new NewLinkData();
        if (data.ChannelIds.Contains(chId)) data.ChannelIds.Remove(chId); else data.ChannelIds.Add(chId);
        await _fsm.SetAsync(cb.From.Id, Flows.NewLink, "channels", data, ct);

        var chans = await _db.Channels.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Title).ToListAsync(ct);
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var ch in chans)
        {
            var marked = data.ChannelIds.Contains(ch.ChatId) ? "✅ " : "▫️ ";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(marked + ch.Title, $"a:nl:ch:{ch.ChatId}") });
        }
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✔️ انتخاب نهایی", "a:nl:ch:done"),
                         InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") });
        await Edit(cb, Strings.NewLink_AskChannels, new InlineKeyboardMarkup(rows), ct);
    }

    async Task NewLink_ChannelsDone(CallbackQuery cb, CancellationToken ct)
    {
        var st = await _fsm.GetAsync(cb.From.Id, ct);
        if (st is null) return;
        var data = FsmStore.DeserializeData<NewLinkData>(st) ?? new NewLinkData();
        if (data.ChannelIds.Count == 0) { await Answer(cb, "حداقل یک کانال انتخاب کنید.", ct); return; }
        await _fsm.SetAsync(cb.From.Id, Flows.NewLink, "name", data, ct);
        await Edit(cb, Strings.NewLink_AskName, AdminPanel.CancelOnly(), ct);
    }

    // ===================== ADD ADMIN =====================
    async Task StartAddAdminAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.AddAdmin, "tgid", new AddAdminData(), ct);
        await Edit(cb, "آی‌دی عددی مدیر جدید را وارد کنید:", AdminPanel.CancelOnly(), ct);
    }

    async Task AddAdmin_ReceiveText(Message m, FsmState st, CancellationToken ct)
    {
        var data = FsmStore.DeserializeData<AddAdminData>(st) ?? new AddAdminData();
        if (st.Step == "tgid")
        {
            if (!long.TryParse((m.Text ?? "").Trim(), out var tg)) { await Reply(m.Chat.Id, "❌ نامعتبر.", ct); return; }
            data.TgId = tg;
            await _fsm.SetAsync(m.From!.Id, Flows.AddAdmin, "role", data, ct);
            var kb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Superadmin", $"a:role:{AdminRoles.Superadmin}") },
                new[] { InlineKeyboardButton.WithCallbackData("Content Manager", $"a:role:{AdminRoles.ContentManager}") },
                new[] { InlineKeyboardButton.WithCallbackData("Viewer", $"a:role:{AdminRoles.Viewer}") },
                new[] { InlineKeyboardButton.WithCallbackData(Strings.Btn_Cancel, "a:cancel") }
            });
            await Reply(m.Chat.Id, "نقش را انتخاب کنید:", ct, kb);
        }
    }

    async Task AddAdmin_PickRole(CallbackQuery cb, string role, CancellationToken ct)
    {
        if (!AdminRoles.IsValid(role)) return;
        var st = await _fsm.GetAsync(cb.From.Id, ct);
        if (st is null) return;
        var data = FsmStore.DeserializeData<AddAdminData>(st) ?? new AddAdminData();
        data.Role = role;
        var existing = await _db.Admins.FirstOrDefaultAsync(a => a.TgId == data.TgId, ct);
        if (existing is null)
            _db.Admins.Add(new AdminEntity { TgId = data.TgId, Role = role, AddedBy = cb.From.Id, AddedAt = DateTime.UtcNow });
        else existing.Role = role;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(cb.From.Id, "admin.add", data.TgId.ToString(), role, ct);
        await _fsm.ClearAsync(cb.From.Id, ct);
        await Edit(cb, Strings.Generic_Saved, AdminPanel.MainMenu(true), ct);
    }

    // ===================== BROADCAST =====================
    async Task StartBroadcastAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.Broadcast, "msg", new BroadcastData(), ct);
        await Edit(cb, Strings.Broadcast_AskMessage, AdminPanel.CancelOnly(), ct);
    }

    async Task Broadcast_ReceiveMessage(Message m, FsmState st, CancellationToken ct)
    {
        // Save the message into storage (so we can broadcast media uniformly via copyMessage)
        int storageMsgId;
        try { storageMsgId = (await _delivery.CopyToStorageAsync(m.Chat.Id, m.Id, ct)).Id; }
        catch { await Reply(m.Chat.Id, "❌ کپی به کانال ذخیره‌سازی شکست خورد.", ct); return; }

        var bc = new Broadcast
        {
            CreatedBy = m.From!.Id, CreatedAt = DateTime.UtcNow,
            Text = m.Text ?? m.Caption ?? "", StorageMsgId = storageMsgId,
            Status = BroadcastStatus.Running, StartedAt = DateTime.UtcNow
        };
        _db.Broadcasts.Add(bc);
        await _db.SaveChangesAsync(ct);

        var users = await _db.Users.AsNoTracking().Where(u => !u.Banned).Select(u => u.TgId).ToListAsync(ct);
        foreach (var uid in users)
            await _jobs.EnqueueAsync(new BroadcastJob(bc.Id, uid, storageMsgId), ct);

        await _audit.LogAsync(m.From.Id, "broadcast.start", bc.Id.ToString(), $"users={users.Count}", ct);
        await _fsm.ClearAsync(m.From.Id, ct);
        await Reply(m.Chat.Id, Strings.Broadcast_Started + $" (n={users.Count})", ct);
    }

    // ===================== SET WELCOME =====================
    async Task StartSetWelcomeAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.SetWelcome, "msg", null, ct);
        await Edit(cb, Strings.SetWelcome_Prompt, AdminPanel.CancelOnly(), ct);
    }

    async Task SetWelcome_ReceiveMessage(Message m, FsmState st, CancellationToken ct)
    {
        var text = m.Text ?? m.Caption ?? "";
        var ents = m.Entities ?? m.CaptionEntities;

        var welcome = await _db.Settings.FirstOrDefaultAsync(s => s.Key == SettingKeys.WelcomeText, ct);
        if (welcome is null)
            _db.Settings.Add(new Setting { Key = SettingKeys.WelcomeText, Value = text, UpdatedAt = DateTime.UtcNow });
        else { welcome.Value = text; welcome.UpdatedAt = DateTime.UtcNow; }

        var entSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == SettingKeys.WelcomeEntities, ct);
        var entJson = ents is null ? "" : JsonSerializer.Serialize(ents);
        if (entSetting is null)
            _db.Settings.Add(new Setting { Key = SettingKeys.WelcomeEntities, Value = entJson, UpdatedAt = DateTime.UtcNow });
        else { entSetting.Value = entJson; entSetting.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(m.From!.Id, "settings.welcome", null, null, ct);
        await _fsm.ClearAsync(m.From.Id, ct);
        await Reply(m.Chat.Id, Strings.SetWelcome_Saved, ct);
    }

    // ===================== LIST commands =====================
    async Task ListContentAsync(CallbackQuery cb, CancellationToken ct)
    {
        var items = await _content.ListAsync(20, ct);
        var sb = new StringBuilder("📁 محتواهای اخیر:\n");
        foreach (var c in items) sb.AppendLine($"• <code>{c.Uuid}</code> — {EscapeHtml(c.Title)}");
        if (items.Count == 0) sb.AppendLine("— خالی —");
        await Edit(cb, sb.ToString(), AdminPanel.ContentMenu(), ct, ParseMode.Html);
    }
    async Task ListChannelsAsync(CallbackQuery cb, CancellationToken ct)
    {
        var items = await _db.Channels.AsNoTracking().OrderBy(c => c.Title).ToListAsync(ct);
        var sb = new StringBuilder("📢 کانال‌ها:\n");
        foreach (var c in items) sb.AppendLine($"• {EscapeHtml(c.Title)} — <code>{c.ChatId}</code> {(c.Active ? "" : "(غیرفعال)")}");
        if (items.Count == 0) sb.AppendLine("— خالی —");
        await Edit(cb, sb.ToString(), AdminPanel.ChannelsMenu(), ct, ParseMode.Html);
    }
    async Task ListLinksAsync(CallbackQuery cb, CancellationToken ct)
    {
        var items = await _db.Links.AsNoTracking().OrderByDescending(l => l.CreatedAt).Take(20).ToListAsync(ct);
        var sb = new StringBuilder("🔗 لینک‌ها:\n");
        foreach (var l in items)
            sb.AppendLine($"• <code>{l.LinkId}</code> — {EscapeHtml(l.Name ?? "")} (v={l.Views}/d={l.Deliveries}){(l.Active ? "" : " 🚫")}");
        if (items.Count == 0) sb.AppendLine("— خالی —");
        await Edit(cb, sb.ToString(), AdminPanel.LinksMenu(), ct, ParseMode.Html);
    }
    async Task ListAdminsAsync(CallbackQuery cb, CancellationToken ct)
    {
        var items = await _db.Admins.AsNoTracking().ToListAsync(ct);
        var sb = new StringBuilder("👥 مدیران:\n");
        foreach (var a in items) sb.AppendLine($"• <code>{a.TgId}</code> — {a.Role}");
        await Edit(cb, sb.ToString(), AdminPanel.AdminsMenu(), ct, ParseMode.Html);
    }

    // ===================== Single-step text flows =====================
    async Task StartDelContentAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.DelContent, "uuid", null, ct);
        await Edit(cb, "UUID محتوای حذف را وارد کنید:", AdminPanel.CancelOnly(), ct);
    }
    async Task DelContent_Receive(Message m, CancellationToken ct)
    {
        var uuid = (m.Text ?? "").Trim();
        await _content.SoftDeleteAsync(uuid, ct);
        await _audit.LogAsync(m.From!.Id, "content.delete", uuid, null, ct);
        await _fsm.ClearAsync(m.From.Id, ct);
        await Reply(m.Chat.Id, Strings.Generic_Deleted, ct);
    }

    async Task StartDeactLinkAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.DeactLink, "linkid", null, ct);
        await Edit(cb, "Link ID را وارد کنید:", AdminPanel.CancelOnly(), ct);
    }
    async Task DeactLink_Receive(Message m, CancellationToken ct)
    {
        var id = (m.Text ?? "").Trim();
        var l = await _db.Links.FirstOrDefaultAsync(x => x.LinkId == id, ct);
        if (l is null) { await Reply(m.Chat.Id, Strings.Generic_NotFound, ct); return; }
        l.Active = !l.Active;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(m.From!.Id, l.Active ? "link.activate" : "link.deactivate", id, null, ct);
        await _fsm.ClearAsync(m.From.Id, ct);
        await Reply(m.Chat.Id, l.Active ? "✅ فعال شد." : "🚫 غیرفعال شد.", ct);
    }

    async Task StartSearchUserAsync(CallbackQuery cb, CancellationToken ct)
    {
        await _fsm.SetAsync(cb.From.Id, Flows.SearchUser, "q", null, ct);
        await Edit(cb, "آی‌دی عددی یا یوزرنیم کاربر:", AdminPanel.CancelOnly(), ct);
    }
    async Task SearchUser_Receive(Message m, CancellationToken ct)
    {
        var q = (m.Text ?? "").Trim().TrimStart('@');
        UserEntity? u = null;
        if (long.TryParse(q, out var tg)) u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.TgId == tg, ct);
        u ??= await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == q, ct);
        await _fsm.ClearAsync(m.From!.Id, ct);
        if (u is null) { await Reply(m.Chat.Id, Strings.Generic_NotFound, ct); return; }
        var deliveries = await _db.Deliveries.CountAsync(d => d.UserTgId == u.TgId, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"👤 <code>{u.TgId}</code> @{EscapeHtml(u.Username ?? "")}");
        sb.AppendLine($"نام: {EscapeHtml((u.FirstName + " " + u.LastName).Trim())}");
        sb.AppendLine($"اولین بازدید: {u.FirstSeen:u}");
        sb.AppendLine($"معرف: {(u.ReferrerTgId?.ToString() ?? "—")}");
        sb.AppendLine($"دریافت محتوا: {deliveries}");
        sb.AppendLine($"بن: {(u.Banned ? "بله" : "خیر")}");
        await Reply(m.Chat.Id, sb.ToString(), ct, parseMode: ParseMode.Html);
    }

    async Task StartBanUserAsync(CallbackQuery cb, CancellationToken ct)
    { await _fsm.SetAsync(cb.From.Id, Flows.BanUser, "tg", null, ct); await Edit(cb, "آی‌دی عددی برای بن:", AdminPanel.CancelOnly(), ct); }
    async Task BanUser_Receive(Message m, AdminEntity adm, CancellationToken ct)
    {
        if (!long.TryParse((m.Text ?? "").Trim(), out var tg)) { await Reply(m.Chat.Id, "❌ نامعتبر.", ct); return; }
        var u = await _db.Users.FirstOrDefaultAsync(x => x.TgId == tg, ct);
        if (u is null) { await Reply(m.Chat.Id, Strings.Generic_NotFound, ct); return; }
        u.Banned = true; await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(adm.TgId, "user.ban", tg.ToString(), null, ct);
        await _fsm.ClearAsync(adm.TgId, ct);
        await Reply(m.Chat.Id, "⛔️ بن شد.", ct);
    }
    async Task StartUnbanUserAsync(CallbackQuery cb, CancellationToken ct)
    { await _fsm.SetAsync(cb.From.Id, Flows.UnbanUser, "tg", null, ct); await Edit(cb, "آی‌دی عددی برای آنبن:", AdminPanel.CancelOnly(), ct); }
    async Task UnbanUser_Receive(Message m, AdminEntity adm, CancellationToken ct)
    {
        if (!long.TryParse((m.Text ?? "").Trim(), out var tg)) { await Reply(m.Chat.Id, "❌ نامعتبر.", ct); return; }
        var u = await _db.Users.FirstOrDefaultAsync(x => x.TgId == tg, ct);
        if (u is null) { await Reply(m.Chat.Id, Strings.Generic_NotFound, ct); return; }
        u.Banned = false; await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(adm.TgId, "user.unban", tg.ToString(), null, ct);
        await _fsm.ClearAsync(adm.TgId, ct);
        await Reply(m.Chat.Id, "✅ آنبن شد.", ct);
    }

    // ===================== Stats / Audit =====================
    async Task ShowStatsAsync(CallbackQuery cb, CancellationToken ct)
    {
        var totalUsers = await _db.Users.CountAsync(ct);
        var totalLinks = await _db.Links.CountAsync(ct);
        var totalContent = await _db.Contents.CountAsync(c => !c.Deleted, ct);
        var totalDeliveries = await _db.Deliveries.CountAsync(ct);
        var totalViews = await _db.LinkViews.CountAsync(ct);
        var top = await _db.Links.AsNoTracking().OrderByDescending(l => l.Deliveries).Take(5)
            .Select(l => new { l.LinkId, l.Name, l.Deliveries, l.Views }).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("📊 آمار کلی");
        sb.AppendLine($"کاربران: {totalUsers}");
        sb.AppendLine($"محتوا: {totalContent}");
        sb.AppendLine($"لینک: {totalLinks}");
        sb.AppendLine($"بازدید لینک: {totalViews}");
        sb.AppendLine($"تحویل موفق: {totalDeliveries}");
        sb.AppendLine($"نرخ تبدیل: {(totalViews == 0 ? "—" : $"{100.0 * totalDeliveries / totalViews:F1}%")}");
        sb.AppendLine();
        sb.AppendLine("🏆 برترین لینک‌ها:");
        foreach (var l in top) sb.AppendLine($"• <code>{l.LinkId}</code> {EscapeHtml(l.Name ?? "")} ({l.Deliveries}/{l.Views})");
        await Edit(cb, sb.ToString(), AdminPanel.MainMenu(true), ct, ParseMode.Html);
    }
    async Task ShowAuditAsync(CallbackQuery cb, CancellationToken ct)
    {
        var items = await _db.AuditLog.AsNoTracking().OrderByDescending(a => a.At).Take(15).ToListAsync(ct);
        var sb = new StringBuilder("📜 آخرین لاگ‌ها:\n");
        foreach (var a in items) sb.AppendLine($"{a.At:u} | {a.AdminTgId} | {a.Action} | {a.Target ?? ""}");
        await Edit(cb, sb.ToString(), AdminPanel.MainMenu(true), ct);
    }

    // ===================== helpers =====================
    Task Reply(long chatId, string text, CancellationToken ct, InlineKeyboardMarkup? kb = null, ParseMode parseMode = default)
        => _sender.ExecuteAsync(chatId, (c, t) => c.SendMessage(chatId, text, parseMode: parseMode, replyMarkup: kb, cancellationToken: t), ct);

    async Task Edit(CallbackQuery cb, string text, InlineKeyboardMarkup kb, CancellationToken ct, ParseMode parseMode = default)
    {
        try
        {
            await _sender.ExecuteAsync(cb.Message!.Chat.Id,
                (c, t) => c.EditMessageText(cb.Message.Chat.Id, cb.Message.Id, text, parseMode: parseMode, replyMarkup: kb, cancellationToken: t), ct);
        }
        catch (ApiRequestException) { /* message identical or too old — fall back to a new message */
            await Reply(cb.Message!.Chat.Id, text, ct, kb, parseMode);
        }
        try { await _sender.ExecuteAsync(null, (c, t) => c.AnswerCallbackQuery(cb.Id, cancellationToken: t), ct); } catch { }
    }

    Task Answer(CallbackQuery cb, string text, CancellationToken ct)
        => _sender.ExecuteAsync(null, (c, t) => c.AnswerCallbackQuery(cb.Id, text, showAlert: true, cancellationToken: t), ct);

    static string EscapeHtml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
