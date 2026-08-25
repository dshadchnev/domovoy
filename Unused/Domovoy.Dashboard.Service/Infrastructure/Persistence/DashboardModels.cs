using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Dashboard.Service.Infrastructure.Persistence;

// Переведено: Read-only Р СР С•Р Т‘Р ВµР В»Р С‘ Р Т‘Р В»РЎРЏ РЎРѓРЎС“РЎвЂ°Р ВµРЎРѓРЎвЂљР Р†РЎС“РЎР‹РЎвЂ°Р С‘РЎвЂ¦ РЎвЂљР В°Р В±Р В»Р С‘РЎвЂ
[Table("DeviceCredentials")]
public class DeviceCredential
{
    [Key]
    public Guid Id { get; set; }
    public string NetworkDeviceId { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public string? Name { get; set; }
    public Guid? RoomId { get; set; }
    public string? Protocol { get; set; }
    public string? Endpoint { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

[Table("CommandLogs")]
public class CommandLog
{
    [Key]
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Params { get; set; }
    public string? SourceRuleId { get; set; }
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public string? Protocol { get; set; }
    public string? Endpoint { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[Table("Rules")]
public class Rule
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? CommandParams { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}