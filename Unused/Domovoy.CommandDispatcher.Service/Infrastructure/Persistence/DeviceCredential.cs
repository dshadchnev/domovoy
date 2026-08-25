using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.CommandDispatcher.Service.Infrastructure.Persistence;

/// <summary>
/// РњРѕРґРµР»СЊ-СЂРµРїР»РёРєР° С‚Р°Р±Р»РёС†С‹ DeviceCredentials (РІР»Р°РґРµР»РµС† вЂ” Auth Service).
/// РСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ С‚РѕР»СЊРєРѕ РґР»СЏ С‡С‚РµРЅРёСЏ вЂ” CommandDispatcher РЅРµ РјРёРіСЂРёСЂСѓРµС‚ СЌС‚Сѓ С‚Р°Р±Р»РёС†Сѓ.
/// </summary>
[Table("DeviceCredentials")]
public class DeviceCredential
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string NetworkDeviceId { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    /// <summary>Р’Р»Р°РґРµР»РµС† СѓСЃС‚СЂРѕР№СЃС‚РІР° (РїРѕР»Рµ OwnerUserId РІ Р‘Р” Auth Service)</summary>
    public Guid? OwnerUserId { get; set; }

    public string? Name { get; set; }

    public Guid? RoomId { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>РџСЂРѕС‚РѕРєРѕР» СЃРІСЏР·Рё: HTTP, MQTT, ZIGBEE</summary>
    public string? Protocol { get; set; }

    /// <summary>URL РґР»СЏ HTTP РёР»Рё С‚РѕРїРёРє РґР»СЏ MQTT</summary>
    public string? Endpoint { get; set; }
}
