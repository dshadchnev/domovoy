using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Auth.Service.Data.Entities;

// 🔑 КРИТИЧНО: Наследуем от IdentityUser<Guid>
[Table("Users")]
public class AuthUser : IdentityUser<Guid>
{
    [Column("Username")] public override string? UserName { get; set; }
    [Column("Email")] public override string? Email { get; set; }
    [Column("PasswordHash")] public override string? PasswordHash { get; set; }

    [Column("FirstName")] public string FirstName { get; set; } = string.Empty;
    [Column("LastName")] public string LastName { get; set; } = string.Empty;
    [Column("CreatedAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("LastLoginAt")] public DateTime? LastLoginAt { get; set; }
    [Column("IsActive")] public bool IsActive { get; set; } = true;
}

[Table("Roles")]
public class AuthRole : IdentityRole<Guid>
{
    [Column("Description")] public string Description { get; set; } = string.Empty;
}

[Table("DeviceCredentials")]
public class DeviceCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NetworkDeviceId { get; set; } = default!;
    public string SecretHash { get; set; } = default!;
    public Guid OwnerUserId { get; set; }
    public Guid? RoomId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}