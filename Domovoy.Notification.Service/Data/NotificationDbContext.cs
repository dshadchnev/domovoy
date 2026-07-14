using Microsoft.EntityFrameworkCore;

namespace Domovoy.Notification.Service.Data;

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
            entity.HasIndex(s => new { s.UserId, s.EventType }).IsUnique();
        });

        modelBuilder.Entity<UserNotificationChannel>(entity =>
        {
            entity.HasIndex(c => new { c.UserId, c.ChannelType, c.ChannelValue }).IsUnique();
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasIndex(l => l.UserId);
            entity.HasIndex(l => l.CreatedAt);
        });
    }
}