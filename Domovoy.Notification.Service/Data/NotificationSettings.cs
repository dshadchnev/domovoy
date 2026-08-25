using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Notification.Service.Data;

[Table("notificationsettings")]
public class NotificationSetting
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EventType { get; set; } = string.Empty; 

    public bool TelegramEnabled { get; set; }
    public bool EmailEnabled { get; set; }

    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? RecipientEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

[Table("usernotificationchannels")]
public class UserNotificationChannel
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ChannelType { get; set; } = string.Empty; 

    [Required, MaxLength(500)]
    public string ChannelValue { get; set; } = string.Empty;  

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("notificationlogs")]
public class NotificationLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Channel { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending"; 

    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}