using Microsoft.EntityFrameworkCore;

namespace Domovoy.CommandDispatcher.Service.Infrastructure.Persistence;

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
        // CommandLogs вЂ” СЃРѕР±СЃС‚РІРµРЅРЅР°СЏ С‚Р°Р±Р»РёС†Р° СЌС‚РѕРіРѕ СЃРµСЂРІРёСЃР°
        modelBuilder.Entity<CommandLog>(entity =>
        {
            entity.Ignore(c => c.MessageId);
            entity.HasIndex(c => new { c.DeviceId, c.CreatedAt });
            entity.HasIndex(c => c.Status);
            entity.Property(c => c.Status).HasMaxLength(20);
            entity.Property(c => c.Protocol).HasMaxLength(20);
        });

        // DeviceCredentials вЂ” С‚Р°Р±Р»РёС†Р° Auth Service, С‚РѕР»СЊРєРѕ РґР»СЏ С‡С‚РµРЅРёСЏ.
        // ExcludeFromMigrations() РіР°СЂР°РЅС‚РёСЂСѓРµС‚ С‡С‚Рѕ CommandDispatcher РЅРµ Р±СѓРґРµС‚ РµС‘ РїРµСЂРµСЃРѕР·РґР°РІР°С‚СЊ.
        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("DeviceCredentials", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NetworkDeviceId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.NetworkDeviceId).IsUnique();
        });
    }
}