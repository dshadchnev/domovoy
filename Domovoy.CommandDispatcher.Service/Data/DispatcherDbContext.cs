using Microsoft.EntityFrameworkCore;

namespace Domovoy.CommandDispatcher.Service.Data;

public class DispatcherDbContext : DbContext
{
    public DispatcherDbContext(DbContextOptions<DispatcherDbContext> options) : base(options) { }

    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();

    /// <summary>
    /// Read-only view of DeviceCredentials table managed by Auth Service.
    /// </summary>
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CommandLogs — собственная таблица этого сервиса
        modelBuilder.Entity<CommandLog>(entity =>
        {
            entity.HasIndex(c => new { c.DeviceId, c.CreatedAt });
            entity.HasIndex(c => c.Status);
            entity.Property(c => c.Status).HasMaxLength(20);
            entity.Property(c => c.Protocol).HasMaxLength(20);
        });

        // DeviceCredentials — таблица Auth Service, только для чтения.
        // ExcludeFromMigrations() гарантирует что CommandDispatcher не будет её пересоздавать.
        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("DeviceCredentials", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NetworkDeviceId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.NetworkDeviceId).IsUnique();
        });
    }
}