using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.CommandDispatcher.Service.Data;

/// <summary>
/// Модель-реплика таблицы DeviceCredentials (владелец — Auth Service).
/// Используется только для чтения — CommandDispatcher не мигрирует эту таблицу.
/// </summary>
[Table("DeviceCredentials")]
public class DeviceCredential
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string NetworkDeviceId { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    /// <summary>Владелец устройства (поле OwnerUserId в БД Auth Service)</summary>
    public Guid? OwnerUserId { get; set; }

    public string? Name { get; set; }

    public Guid? RoomId { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Протокол связи: HTTP, MQTT, ZIGBEE</summary>
    public string? Protocol { get; set; }

    /// <summary>URL для HTTP или топик для MQTT</summary>
    public string? Endpoint { get; set; }
}
