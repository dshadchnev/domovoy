using Telegram.Bot;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Infrastructure.External;

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
        await SendCustomAsync(null, chatId, subject, message);
    }

    public async Task SendCustomAsync(string? customBotToken, string chatId, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            throw new InvalidOperationException("Telegram Chat ID is not configured");

        var botClient = !string.IsNullOrWhiteSpace(customBotToken)
            ? new TelegramBotClient(customBotToken)
            : _botClient;

        if (botClient == null)
            throw new InvalidOperationException("Telegram Bot Token is not configured");

        var fullMessage = $"🔔 *{subject}*\n\n{message}";

        await botClient.SendMessage(
            chatId: chatId,
            text: fullMessage,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

        _logger.LogInformation("✅ Telegram message sent to Chat ID {ChatId}", chatId);
    }
}