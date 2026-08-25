using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Consumers;

public class CommandFailedConsumer : IConsumer<CommandFailedEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _dbFactory;
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly ILogger<CommandFailedConsumer> _logger;

    public CommandFailedConsumer(
        IDbContextFactory<NotificationDbContext> dbFactory,
        IEnumerable<INotificationSender> senders,
        ILogger<CommandFailedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _senders = senders;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CommandFailedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("Processing CommandFailedEvent for device {DeviceId}", evt.DeviceId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == evt.DeviceId);

        if (device?.OwnerUserId == null)
        {
            _logger.LogWarning("Device {DeviceId} not found or has no owner", evt.DeviceId);
            return;
        }

        var userIdStr = device.OwnerUserId.Value.ToString();

        var settings = await db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userIdStr && s.EventType == "CommandFailed");

        if (settings == null || (!settings.TelegramEnabled && !settings.EmailEnabled))
            return;

        var subject = "Ошибка выполнения команды";
        var message = $"Устройство: {evt.DeviceId}\n" +
                      $"Команда: {evt.Command}\n" +
                      $"Ошибка: {evt.ErrorMessage}";

        var channels = await db.UserNotificationChannels
            .Where(c => c.UserId == userIdStr && c.IsActive)
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

                db.NotificationLogs.Add(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userIdStr,
                    EventType = "CommandFailed",
                    Channel = channel.ChannelType,
                    Message = message,
                    Status = "sent",
                    SentAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for command failure");

                db.NotificationLogs.Add(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userIdStr,
                    EventType = "CommandFailed",
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