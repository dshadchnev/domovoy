using Telegram.Bot;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Services;

public class TelegramSender : INotificationSender
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramSender> _logger;

    public string ChannelType => "Telegram";

    public TelegramSender(ITelegramBotClient botClient, ILogger<TelegramSender> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    public async Task SendAsync(string chatId, string subject, string message)
    {
        var fullMessage = $"🔔 {subject}\n\n{message}";

        await _botClient.SendMessage(
            chatId: chatId,
            text: fullMessage,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

        _logger.LogInformation("✅ Telegram sent to {ChatId}", chatId);
    }
}