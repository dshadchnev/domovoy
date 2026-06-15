using System.ComponentModel.DataAnnotations;

namespace Domovoy.CommandDispatcher.Service.Data;

public class CommandLog
{
	[Key]
	public Guid Id { get; set; }

	[Required, MaxLength(100)]
	public string DeviceId { get; set; } = string.Empty;

	[Required]
	public string Command { get; set; } = string.Empty;  // "turn_on", "set_brightness"

	public string? Params { get; set; }  // JSON: {"brightness": 80}

	public string? SourceRuleId { get; set; }  // Какое правило сработало (для аудита)

	[Required]
	public string Status { get; set; } = "pending";  // pending, sent, success, failed

	public string? ErrorMessage { get; set; }  // Если статус = failed
	public string? Protocol { get; set; }  // HTTP, MQTT, Zigbee

	public string? Endpoint { get; set; }  // URL или топик для отправки

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? SentAt { get; set; }
	public DateTime? CompletedAt { get; set; }
}