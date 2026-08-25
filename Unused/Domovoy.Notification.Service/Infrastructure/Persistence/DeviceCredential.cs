using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Notification.Service.Infrastructure.Persistence;

[Table("DeviceCredentials")]
public class DeviceCredential
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
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
