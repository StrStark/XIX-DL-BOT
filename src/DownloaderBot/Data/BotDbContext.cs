using DownloaderBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using AdminEntity = DownloaderBot.Data.Entities.Admin;
using ContentEntity = DownloaderBot.Data.Entities.Content;

namespace DownloaderBot.Data;

public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AdminEntity> Admins => Set<AdminEntity>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ContentEntity> Contents => Set<ContentEntity>();
    public DbSet<ContentMessage> ContentMessages => Set<ContentMessage>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<Link> Links => Set<Link>();
    public DbSet<LinkTag> LinkTags => Set<LinkTag>();
    public DbSet<LinkContent> LinkContents => Set<LinkContent>();
    public DbSet<LinkChannel> LinkChannels => Set<LinkChannel>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<LinkView> LinkViews => Set<LinkView>();
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<FsmState> FsmStates => Set<FsmState>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.TgId);
            e.Property(x => x.TgId).HasColumnName("tg_id").ValueGeneratedNever();
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.FirstSeen).HasColumnName("first_seen");
            e.Property(x => x.LastSeen).HasColumnName("last_seen");
            e.Property(x => x.ReferrerTgId).HasColumnName("referrer_tg_id");
            e.Property(x => x.Banned).HasColumnName("banned");
            e.HasIndex(x => x.Banned);
        });

        b.Entity<AdminEntity>(e =>
        {
            e.ToTable("admins");
            e.HasKey(x => x.TgId);
            e.Property(x => x.TgId).HasColumnName("tg_id").ValueGeneratedNever();
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.AddedBy).HasColumnName("added_by");
            e.Property(x => x.AddedAt).HasColumnName("added_at");
        });

        b.Entity<Channel>(e =>
        {
            e.ToTable("channels");
            e.HasKey(x => x.ChatId);
            e.Property(x => x.ChatId).HasColumnName("chat_id").ValueGeneratedNever();
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.InviteLink).HasColumnName("invite_link");
            e.Property(x => x.AddedBy).HasColumnName("added_by");
            e.Property(x => x.AddedAt).HasColumnName("added_at");
            e.Property(x => x.Active).HasColumnName("active");
        });

        b.Entity<ContentEntity>(e =>
        {
            e.ToTable("content");
            e.HasKey(x => x.Uuid);
            e.Property(x => x.Uuid).HasColumnName("uuid");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.Deleted).HasColumnName("deleted");
            e.HasIndex(x => x.Deleted);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.ContentUuid).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ContentTags).WithOne().HasForeignKey(ct => ct.ContentUuid).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ContentMessage>(e =>
        {
            e.ToTable("content_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ContentUuid).HasColumnName("content_uuid");
            e.Property(x => x.StorageMsgId).HasColumnName("storage_msg_id");
            e.Property(x => x.OrderIndex).HasColumnName("order_index");
            e.HasIndex(x => new { x.ContentUuid, x.OrderIndex });
        });

        b.Entity<Tag>(e =>
        {
            e.ToTable("tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<ContentTag>(e =>
        {
            e.ToTable("content_tags");
            e.HasKey(x => new { x.ContentUuid, x.TagId });
            e.Property(x => x.ContentUuid).HasColumnName("content_uuid");
            e.Property(x => x.TagId).HasColumnName("tag_id");
        });

        b.Entity<Link>(e =>
        {
            e.ToTable("links");
            e.HasKey(x => x.LinkId);
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.SelectionMode).HasColumnName("selection_mode");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.Active).HasColumnName("active");
            e.Property(x => x.Views).HasColumnName("views");
            e.Property(x => x.Deliveries).HasColumnName("deliveries");
        });

        b.Entity<LinkTag>(e =>
        {
            e.ToTable("link_tags");
            e.HasKey(x => new { x.LinkId, x.TagId });
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.TagId).HasColumnName("tag_id");
        });

        b.Entity<LinkContent>(e =>
        {
            e.ToTable("link_content");
            e.HasKey(x => new { x.LinkId, x.ContentUuid });
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.ContentUuid).HasColumnName("content_uuid");
        });

        b.Entity<LinkChannel>(e =>
        {
            e.ToTable("link_channels");
            e.HasKey(x => new { x.LinkId, x.ChatId });
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.ChatId).HasColumnName("chat_id");
        });

        b.Entity<Delivery>(e =>
        {
            e.ToTable("deliveries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.UserTgId).HasColumnName("user_tg_id");
            e.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
            e.HasIndex(x => new { x.LinkId, x.UserTgId }).IsUnique();
            e.HasIndex(x => x.LinkId);
            e.HasIndex(x => x.UserTgId);
        });

        b.Entity<LinkView>(e =>
        {
            e.ToTable("link_views");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LinkId).HasColumnName("link_id");
            e.Property(x => x.UserTgId).HasColumnName("user_tg_id");
            e.Property(x => x.ViewedAt).HasColumnName("viewed_at");
            e.Property(x => x.Delivered).HasColumnName("delivered");
            e.HasIndex(x => new { x.LinkId, x.ViewedAt });
        });

        b.Entity<Broadcast>(e =>
        {
            e.ToTable("broadcasts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.FinishedAt).HasColumnName("finished_at");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.StorageMsgId).HasColumnName("storage_msg_id");
            e.Property(x => x.SentCount).HasColumnName("sent_count");
            e.Property(x => x.FailedCount).HasColumnName("failed_count");
            e.Property(x => x.Status).HasColumnName("status");
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AdminTgId).HasColumnName("admin_tg_id");
            e.Property(x => x.Action).HasColumnName("action");
            e.Property(x => x.Target).HasColumnName("target");
            e.Property(x => x.Details).HasColumnName("details");
            e.Property(x => x.At).HasColumnName("at");
            e.HasIndex(x => new { x.AdminTgId, x.At });
        });

        b.Entity<FsmState>(e =>
        {
            e.ToTable("fsm_state");
            e.HasKey(x => x.AdminTgId);
            e.Property(x => x.AdminTgId).HasColumnName("admin_tg_id").ValueGeneratedNever();
            e.Property(x => x.Flow).HasColumnName("flow");
            e.Property(x => x.Step).HasColumnName("step");
            e.Property(x => x.Data).HasColumnName("data");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<Setting>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
