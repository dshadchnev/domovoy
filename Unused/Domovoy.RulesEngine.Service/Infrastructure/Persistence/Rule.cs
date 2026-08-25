using System.ComponentModel.DataAnnotations;

namespace Domovoy.RulesEngine.Service.Infrastructure.Persistence;

/// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: Infrastructure entity for an automation rule.
/// РџРµСЂРµРІРµРґРµРЅРѕ: Condition: NCalc string expression (e.g. "temperature > 25")
/// РџРµСЂРµРІРµРґРµРЅРѕ: Command:   string command id (e.g. "turn_on")
/// РџРµСЂРµРІРµРґРµРЅРѕ: SensorDeviceId:   the device whose telemetry triggers the rule
/// РџРµСЂРµРІРµРґРµРЅРѕ: ActuatorDeviceId: the device that receives the command
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
public class Rule
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Legacy / fallback: target device for the command.</summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Device whose telemetry reading triggers the condition.</summary>
    public string? SensorDeviceId { get; set; }

    /// <summary>Device that receives the command when condition is met.</summary>
    public string? ActuatorDeviceId { get; set; }

    /// <summary>NCalc expression: "temperature > 25"</summary>
    [Required]
    public string Condition { get; set; } = string.Empty;

    /// <summary>Command token: "turn_on", "set_brightness", ...</summary>
    [Required]
    public string Command { get; set; } = string.Empty;

    /// <summary>Optional JSON params: {"brightness": 80}</summary>
    public string? CommandParams { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0;

    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
