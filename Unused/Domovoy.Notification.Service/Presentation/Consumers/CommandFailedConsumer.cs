using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Domain.Events;
using Domovoy.Notification.Service.Infrastructure.Persistence;
using Domovoy.Notification.Service.Infrastructure.External;
using Domovoy.Notification.Service.Infrastructure.External.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Presentation.Consumers;

public class CommandFailedConsumer : IConsumer<CommandFailedEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly INotificationAdapterFactory? _adapterFactory;
    private readonly ILogger<CommandFailedConsumer> _logger;

    public CommandFailedConsumer(
        IDbContextFactory<NotificationDbContext> dbFactory,
        IEnumerable<INotificationSender> senders,
        ILogger<CommandFailedConsumer> logger,
        INotificationAdapterFactory? adapterFactory = null)
    {
        _dbFactory = dbFactory;
        _senders = senders;
        _logger = logger;
        _adapterFactory = adapterFactory;
    }

    public async Task Consume(ConsumeContext<CommandFailedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("⚠️ Processing CommandFailedEvent for device {DeviceId}", evt.DeviceId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Переведено: Находим владельца устройства
        var device = await db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == evt.DeviceId);

        if (device?.OwnerUserId == null)
        {
            _logger.LogWarning("️ Device {DeviceId} not found or has no owner", evt.DeviceId);
            return;
        }

        var userIdStr = device.OwnerUserId.Value.ToString();

        var settings = await db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userIdStr && (s.EventType == "CommandFailed" || s.EventType == "All"));

        if (settings == null || (!settings.TelegramEnabled && !settings.EmailEnabled))
            return;

        var subject = "⚠️ Ошибка выполнения команды";
        var message = $"Устройство: {evt.DeviceId}\n" +
                      $"Команда: {evt.Command}\n" +
                      $"Ошибка: {evt.ErrorMessage}";

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
                        UserId = userIdStr, EventType = "CommandFailed", Channel = "Telegram",
                        Message = message, Status = "sent", SentAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send Telegram notification for failed command on {DeviceId}", evt.DeviceId);
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userIdStr, EventType = "CommandFailed", Channel = "Telegram",
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
                        UserId = userIdStr, EventType = "CommandFailed", Channel = "Email",
                        Message = message, Status = "sent", SentAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send Email notification for failed command on {DeviceId}", evt.DeviceId);
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userIdStr, EventType = "CommandFailed", Channel = "Email",
                    Message = message, Status = "failed", ErrorMessage = ex.Message
                });
            }
        }

        await db.SaveChangesAsync();
    }
}