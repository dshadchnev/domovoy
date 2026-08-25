using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domovoy.Domain.Events;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Domovoy.Notification.Service.Infrastructure.External.Adapters;

/// <summary>
/// ACL адаптер для взаимодействия с внешним Telegram Bot API.
/// Переводит доменное намерение NotificationRequested в протокольные вызовы Telegram Bot API.
/// </summary>
public class TelegramNotificationAdapter : INotificationAdapter
{
    private readonly ITelegramBotClient? _botClient;
    private readonly INotificationSender? _notificationSender;
    private readonly ILogger<TelegramNotificationAdapter> _logger;

    public string ChannelType => "Telegram";

    public TelegramNotificationAdapter(
        ILogger<TelegramNotificationAdapter> logger,
        ITelegramBotClient? botClient = null,
        IEnumerable<INotificationSender>? senders = null)
    {
        _logger = logger;
        _botClient = botClient;
        _notificationSender = senders?.FirstOrDefault(s => s.ChannelType == "Telegram");
    }

    public async Task SendNotificationAsync(NotificationRequested notification, CancellationToken cancellationToken = default)
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));

        var fullMessage = $"🔔 {notification.Title}\n\n{notification.Message}";

        if (_botClient != null)
        {
            await _botClient.SendMessage(
                chatId: notification.RecipientAddress,
                text: fullMessage,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);

            _logger.LogInformation("✅ [ACL TelegramAdapter] Sent Telegram notification to {ChatId}", notification.RecipientAddress);
        }
        else if (_notificationSender != null)
        {
            await _notificationSender.SendAsync(notification.RecipientAddress, notification.Title, notification.Message);
            _logger.LogInformation("✅ [ACL TelegramAdapter] Delegated notification send to {Recipient}", notification.RecipientAddress);
        }
        else
        {
            _logger.LogWarning("⚠️ [ACL TelegramAdapter] Neither ITelegramBotClient nor INotificationSender registered for Telegram");
        }
    }
}
