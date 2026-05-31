using System.ComponentModel.DataAnnotations;

namespace Domovoy.RulesEngine.Service.Data;

public class Rule
{
	[Key]
	public Guid Id { get; set; }

	[Required, MaxLength(100)]
	public string Name { get; set; } = string.Empty;
	
	// На какое устройство применяется
	[Required]
	public string DeviceId { get; set; } = string.Empty;  
	
	// Условие: "temperature > 25"
	[Required]
	public string Condition { get; set; } = string.Empty;  
	
	// Команда: "turn_on", "set_brightness"
	[Required]
	public string Command { get; set; } = string.Empty;
	
	// JSON: {"brightness": 80}
	public string? CommandParams { get; set; }             

	public bool IsActive { get; set; } = true;

	// Приоритет выполнения (меньше = выше)
	public int Priority { get; set; } = 0;

	// Владелец правила
	public string? UserId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
}