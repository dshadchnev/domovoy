namespace Domovoy.Notification.Service.Data;

public class NotificationSettingsDto
{
    public bool EmailEnabled { get; set; }
    public bool TelegramEnabled { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? RecipientEmail { get; set; }
}