using Microsoft.EntityFrameworkCore;

namespace Domovoy.Dashboard.Service.Data;

public class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options) { }

    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();
    public DbSet<Rule> Rules => Set<Rule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Все таблицы только для чтения - исключаем из миграций
        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("DeviceCredentials", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.NetworkDeviceId).IsUnique();
        });

        modelBuilder.Entity<CommandLog>(entity =>
        {
            entity.ToTable("CommandLogs", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Rule>(entity =>
        {
            entity.ToTable("Rules", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.DeviceId);
        });
    }
}