using Microsoft.EntityFrameworkCore;

namespace Domovoy.DeviceManager.Service.Data;

public class DeviceManagerDbContext : DbContext
{
    public DeviceManagerDbContext(DbContextOptions<DeviceManagerDbContext> options) : base(options) { }

    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("DeviceCredentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NetworkDeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SecretHash).IsRequired();
            entity.Property(e => e.OwnerUserId).HasColumnName("OwnerUserId");
            entity.Property(e => e.RoomId).HasColumnName("RoomId");
            entity.HasIndex(e => e.NetworkDeviceId).IsUnique();
        });
    }
}

public class DeviceCredential
{
    public Guid Id { get; set; }
    public string NetworkDeviceId { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }  
    public string? Name { get; set; }
    public Guid? RoomId { get; set; }       
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? Protocol { get; set; }
    public string? Endpoint { get; set; }
}