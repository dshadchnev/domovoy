using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Domain.Events;
using Domovoy.Notification.Service.Infrastructure.Persistence;
using Domovoy.Notification.Service.Infrastructure.External;
using Domovoy.Notification.Service.Infrastructure.External.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Presentation.Consumers;

public class RuleTriggeredConsumer : IConsumer<RuleTriggeredEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly INotificationAdapterFactory? _adapterFactory;
    private readonly ILogger<RuleTriggeredConsumer> _logger;

    public RuleTriggeredConsumer(
        IDbContextFactory<NotificationDbContext> dbFactory,
        IEnumerable<INotificationSender> senders,
        ILogger<RuleTriggeredConsumer> logger,
        INotificationAdapterFactory? adapterFactory = null)
    {
        _dbFactory = dbFactory;
        _senders = senders;
        _logger = logger;
        _adapterFactory = adapterFactory;
    }

    public async Task Consume(ConsumeContext<RuleTriggeredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("🔔 Processing RuleTriggeredEvent for user {UserId}", evt.UserId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var userIdStr = evt.UserId.ToString();

        // Переведено: Получаем настройки уведомлений
        var settings = await db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userIdStr && (s.EventType == "RuleTriggered" || s.EventType == "All"));

        if (settings == null || (!settings.TelegramEnabled && !settings.EmailEnabled ))
        {
            _logger.LogInformation("️ No notification settings for user {UserId}", evt.UserId);
            return;
        }

        var subject = $"Правило сработало: {evt.RuleName}";
        var message = $"Устройство: {evt.DeviceId}\n" +
                      $"Значение: {evt.Value}\n" +
                      $"Команда: {evt.Command}";

        // Переведено: 1. Отправка Telegram
        if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            try
            {
                var tgSender = _senders.OfType<TelegramSender>().FirstOrDefault();
                if (tgSender != null)
                {
                    await tgSender.SendCustomAsync(settings.TelegramBotToken, settings.TelegramChatId, subject, message);
                    db.NotificationLogs.Add(new NotificationLog
                    {
                        UserId = userIdStr, EventType = "RuleTriggered", Channel = "Telegram",
                        Message = message, Status = "sent", SentAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send Telegram notification to {ChatId}", settings.TelegramChatId);
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userIdStr, EventType = "RuleTriggered", Channel = "Telegram",
                    Message = message, Status = "failed", ErrorMessage = ex.Message
                });
            }
        }

        // Переведено: 2. Отправка Email
        if (settings.EmailEnabled && !string.IsNullOrWhiteSpace(settings.RecipientEmail))
        {
            try
            {
                var emailSender = _senders.OfType<EmailSender>().FirstOrDefault();
                if (emailSender != null)
                {
                    await emailSender.SendCustomAsync(
                        settings.SmtpHost, settings.SmtpPort, settings.SmtpUser,
                        settings.SmtpPass, settings.SmtpFromEmail, settings.RecipientEmail,
                        subject, message);

                    db.NotificationLogs.Add(new NotificationLog
                    {
                        UserId = userIdStr, EventType = "RuleTriggered", Channel = "Email",
                        Message = message, Status = "sent", SentAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send Email notification to {Email}", settings.RecipientEmail);
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userIdStr, EventType = "RuleTriggered", Channel = "Email",
                    Message = message, Status = "failed", ErrorMessage = ex.Message
                });
            }
        }

        await db.SaveChangesAsync();
    }
}