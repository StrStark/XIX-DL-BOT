namespace DownloaderBot.Data.Entities;

public static class AdminRoles
{
    public const string Superadmin = "superadmin";
    public const string ContentManager = "content_manager";
    public const string Viewer = "viewer";

    public static bool IsValid(string r) => r is Superadmin or ContentManager or Viewer;
}

public sealed class Admin
{
    public long TgId { get; set; }
    public string Role { get; set; } = AdminRoles.Viewer;
    public long? AddedBy { get; set; }
    public DateTime AddedAt { get; set; }
}
