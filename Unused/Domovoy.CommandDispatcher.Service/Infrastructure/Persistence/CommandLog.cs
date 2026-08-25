using System.ComponentModel.DataAnnotations;

namespace Domovoy.CommandDispatcher.Service.Infrastructure.Persistence;

public class CommandLog
{
	[Key]
	public Guid Id { get; set; }

	[Required, MaxLength(100)]
	public string DeviceId { get; set; } = string.Empty;

	[Required]
	// "turn_on", "set_brightness"
	public string Command { get; set; } = string.Empty;  
	// JSON: {"brightness": 80}
	public string? Params { get; set; }  
	// Какое правило сработало (для аудита)
	public string? SourceRuleId { get; set; }  

	// Уникальный идентификатор сообщения MassTransit для идемпотентности
	[MaxLength(100)]
	public string? MessageId { get; set; }

	[Required]
	// pending, sent, success, failed
	public string Status { get; set; } = "pending";  
	// Если статус = failed
	public string? ErrorMessage { get; set; }
    // HTTP, MQTT, Zigbee
    public string? Protocol { get; set; }
    // URL или топик для отправки
    public string? Endpoint { get; set; }  

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? SentAt { get; set; }
	public DateTime? CompletedAt { get; set; }
}