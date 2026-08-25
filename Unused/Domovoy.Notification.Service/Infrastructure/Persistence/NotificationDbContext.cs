using Microsoft.EntityFrameworkCore;

namespace Domovoy.Notification.Service.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<UserNotificationChannel> UserNotificationChannels => Set<UserNotificationChannel>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("DeviceCredentials", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.NetworkDeviceId).IsUnique();
        });

        modelBuilder.Entity<NotificationSetting>(entity =>
        {
            entity.ToTable("notificationsettings");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("userid");
            entity.Property(e => e.EventType).HasColumnName("eventtype");
            entity.Property(e => e.TelegramEnabled).HasColumnName("telegramenabled");
            entity.Property(e => e.EmailEnabled).HasColumnName("emailenabled");
            entity.Property(e => e.TelegramBotToken).HasColumnName("telegrambottoken");
            entity.Property(e => e.TelegramChatId).HasColumnName("telegramchatid");
            entity.Property(e => e.SmtpHost).HasColumnName("smtphost");
            entity.Property(e => e.SmtpPort).HasColumnName("smtpport");
            entity.Property(e => e.SmtpUser).HasColumnName("smtpuser");
            entity.Property(e => e.SmtpPass).HasColumnName("smtppass");
            entity.Property(e => e.SmtpFromEmail).HasColumnName("smtpfromemail");
            entity.Property(e => e.RecipientEmail).HasColumnName("recipientemail");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedat");
            entity.HasIndex(s => new { s.UserId, s.EventType }).IsUnique();
        });

        modelBuilder.Entity<UserNotificationChannel>(entity =>
        {
            entity.ToTable("usernotificationchannels");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("userid");
            entity.Property(e => e.ChannelType).HasColumnName("channeltype");
            entity.Property(e => e.ChannelValue).HasColumnName("channelvalue");
            entity.Property(e => e.IsActive).HasColumnName("isactive");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat");
            entity.HasIndex(c => new { c.UserId, c.ChannelType, c.ChannelValue }).IsUnique();
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("notificationlogs");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("userid");
            entity.Property(e => e.EventType).HasColumnName("eventtype");
            entity.Property(e => e.Channel).HasColumnName("channel");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ErrorMessage).HasColumnName("errormessage");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat");
            entity.Property(e => e.SentAt).HasColumnName("sentat");
            entity.HasIndex(l => l.UserId);
            entity.HasIndex(l => l.CreatedAt);
        });
    }
}