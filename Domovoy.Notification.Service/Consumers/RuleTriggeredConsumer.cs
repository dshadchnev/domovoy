using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Consumers;

public class RuleTriggeredConsumer : IConsumer<RuleTriggeredEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly ILogger<RuleTriggeredConsumer> _logger;

    public RuleTriggeredConsumer(
        IDbContextFactory<NotificationDbContext> dbFactory,
        IEnumerable<INotificationSender> senders,
        ILogger<RuleTriggeredConsumer> logger)
    {
        _dbFactory = dbFactory;
        _senders = senders;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RuleTriggeredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("🔔 Processing RuleTriggeredEvent for user {UserId}", evt.UserId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Получаем настройки уведомлений
        var settings = await db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == evt.UserId && s.EventType == "RuleTriggered");

        if (settings == null || (!settings.TelegramEnabled && !settings.EmailEnabled ))
        {
            _logger.LogInformation("️ No notification settings for user {UserId}", evt.UserId);
            return;
        }

        var subject = $"Правило сработало: {evt.RuleName}";
        var message = $"Устройство: {evt.DeviceId}\n" +
                      $"Значение: {evt.Value}\n" +
                      $"Команда: {evt.Command}";

        // Получаем каналы пользователя
        var channels = await db.UserNotificationChannels
            .Where(c => c.UserId == evt.UserId && c.IsActive)
            .ToListAsync();

        foreach (var channel in channels)
        {
            try
            {
                var sender = _senders.FirstOrDefault(s => s.ChannelType == channel.ChannelType);
                if (sender == null) continue;

                if (channel.ChannelType == "Telegram" && !settings.TelegramEnabled) continue;
                if (channel.ChannelType == "Email" && !settings.EmailEnabled) continue;

                await sender.SendAsync(channel.ChannelValue, subject, message);

                // Логируем успешную отправку
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = evt.UserId,
                    EventType = "RuleTriggered",
                    Channel = channel.ChannelType,
                    Message = message,
                    Status = "sent",
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send {Channel} notification to {Value}",
                    channel.ChannelType, channel.ChannelValue);

                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = evt.UserId,
                    EventType = "RuleTriggered",
                    Channel = channel.ChannelType,
                    Message = message,
                    Status = "failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        await db.SaveChangesAsync();
    }
}