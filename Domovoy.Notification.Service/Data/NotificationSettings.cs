using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Notification.Service.Data;

[Table("NotificationSettings")]
public class NotificationSetting
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required, MaxLength(50)]
    // "RuleTriggered", "CommandFailed", "DeviceOffline"
    public string EventType { get; set; } = string.Empty; 

    [Required]
    public bool TelegramEnabled { get; set; }

    [Required]
    public bool EmailEnabled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

[Table("UserNotificationChannels")]
public class UserNotificationChannel
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required, MaxLength(20)]
    // "Telegram", "Email"
    public string ChannelType { get; set; } = string.Empty; 

    [Required, MaxLength(500)]
    // Telegram chat_id, email address
    public string ChannelValue { get; set; } = string.Empty;  

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("NotificationLogs")]
public class NotificationLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required, MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Channel { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    // pending, sent, failed
    public string Status { get; set; } = "pending"; 

    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}