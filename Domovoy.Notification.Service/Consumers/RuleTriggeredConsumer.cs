using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Domovoy.Notification.Service.Consumers;

public class RuleTriggeredConsumer : IConsumer<RuleTriggeredEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RuleTriggeredConsumer> _logger;

    public RuleTriggeredConsumer(
        IDbContextFactory<NotificationDbContext> dbFactory,
        IConfiguration configuration,
        ILogger<RuleTriggeredConsumer> logger)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RuleTriggeredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("Received RuleTriggeredEvent: Rule='{RuleName}', Device='{DeviceId}', Val='{Value}', Cmd='{Command}'",
            evt.RuleName, evt.DeviceId, evt.Value, evt.Command);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var userIdStr = evt.UserId.ToString();

        var settings = await db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userIdStr && s.EventType == "RuleTriggered");

        if (settings == null)
        {
            settings = await db.NotificationSettings
                .FirstOrDefaultAsync(s => s.EventType == "RuleTriggered" && (s.EmailEnabled || s.TelegramEnabled));
        }

        if (settings == null || (!settings.TelegramEnabled && !settings.EmailEnabled))
        {
            _logger.LogInformation("No active notification settings found for RuleTriggered");
            return;
        }

        var message = $"Устройство: {evt.DeviceId}\n" +
                      $"Значение: {evt.Value}\n" +
                      $"Команда: {evt.Command}";

        // 1. Email notification
        if (settings.EmailEnabled && !string.IsNullOrWhiteSpace(settings.RecipientEmail))
        {
            try
            {
                var host = !string.IsNullOrWhiteSpace(settings.SmtpHost) ? settings.SmtpHost : (_configuration["Smtp:Host"] ?? "smtp.mail.ru");
                var port = settings.SmtpPort ?? (int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 465);
                var user = !string.IsNullOrWhiteSpace(settings.SmtpUser) ? settings.SmtpUser : (_configuration["Smtp:User"] ?? "");
                var pass = !string.IsNullOrWhiteSpace(settings.SmtpPass) ? settings.SmtpPass : (_configuration["Smtp:Pass"] ?? "");
                var from = settings.SmtpFromEmail;
                if (string.IsNullOrWhiteSpace(from) || from.EndsWith(".local") || !from.Contains("@"))
                {
                    from = !string.IsNullOrWhiteSpace(user) && user.Contains("@") ? user : settings.RecipientEmail;
                }

                var html = $"<h3> Сработало правило автоматизации «{evt.RuleName}»</h3>" +
                           $"<p><b>Устройство:</b> {evt.DeviceId}</p>" +
                           $"<p><b>Значение телеметрии:</b> {evt.Value}</p>" +
                           $"<p><b>Выполненная команда:</b> {evt.Command}</p>" +
                           $"<hr/><small>Время события (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</small>";

                await EmailSender.SendEmailInternalAsync(host, port, user, pass, from, settings.RecipientEmail, $"[Домовой] Сработало правило: {evt.RuleName}", html);

                db.NotificationLogs.Add(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = settings.UserId,
                    EventType = "RuleTriggered",
                    Channel = "Email",
                    Message = message,
                    Status = "sent",
                    SentAt = DateTime.UtcNow
                });
                _logger.LogInformation("[Success] Email rule notification delivered to {Email}", settings.RecipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification for RuleTriggered to {Email}", settings.RecipientEmail);
                db.NotificationLogs.Add(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = settings.UserId,
                    EventType = "RuleTriggered",
                    Channel = "Email",
                    Message = message,
                    Status = "failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        // 2. Telegram notification
        if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            try
            {
                var botToken = !string.IsNullOrWhiteSpace(settings.TelegramBotToken) ? settings.TelegramBotToken : _configuration["Telegram:BotToken"];
                if (!string.IsNullOrEmpty(botToken))
                {
                    var bot = new TelegramBotClient(botToken);
                    var text = $" *Сработало правило*: {evt.RuleName}\n\n" +
                               $" *Устройство*: `{evt.DeviceId}`\n" +
                               $" *Значение*: `{evt.Value}`\n" +
                               $" *Команда*: `{evt.Command}`";

                    await bot.SendMessage(settings.TelegramChatId, text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                    db.NotificationLogs.Add(new NotificationLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = settings.UserId,
                        EventType = "RuleTriggered",
                        Channel = "Telegram",
                        Message = message,
                        Status = "sent",
                        SentAt = DateTime.UtcNow
                    });
                    _logger.LogInformation(" [Success] Telegram rule notification delivered to {ChatId}", settings.TelegramChatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Failed to send Telegram notification to {ChatId}", settings.TelegramChatId);
                db.NotificationLogs.Add(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = settings.UserId,
                    EventType = "RuleTriggered",
                    Channel = "Telegram",
                    Message = message,
                    Status = "failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        await db.SaveChangesAsync();
    }
}