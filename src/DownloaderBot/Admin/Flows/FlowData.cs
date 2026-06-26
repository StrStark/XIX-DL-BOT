namespace DownloaderBot.Admin.Flows;

public static class Flows
{
    public const string AddContent = "addcontent";
    public const string AddChannel = "addchannel";
    public const string NewLink = "newlink";
    public const string AddAdmin = "addadmin";
    public const string Broadcast = "broadcast";
    public const string SetWelcome = "setwelcome";
    public const string DelContent = "delcontent";
    public const string DeactLink = "deactlink";
    public const string SearchUser = "searchuser";
    public const string BanUser = "banuser";
    public const string UnbanUser = "unbanuser";
    public const string DelAdmin = "deladmin";
}

public sealed class AddContentData
{
    public List<int> StorageMsgIds { get; set; } = new();
    public string? MediaGroupId { get; set; }
    public DateTime LastMsgAt { get; set; }
    public string? Title { get; set; }
}

public sealed class AddChannelData
{
    public long ChatId { get; set; }
    public string? Title { get; set; }
    public string? InviteLink { get; set; }
}

public sealed class NewLinkData
{
    public string Mode { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<string> Uuids { get; set; } = new();
    public List<long> ChannelIds { get; set; } = new();
    public string? Name { get; set; }
}

public sealed class BroadcastData
{
    public string? Text { get; set; }
    public int? StorageMsgId { get; set; }
}

public sealed class AddAdminData
{
    public long TgId { get; set; }
    public string Role { get; set; } = "viewer";
}
