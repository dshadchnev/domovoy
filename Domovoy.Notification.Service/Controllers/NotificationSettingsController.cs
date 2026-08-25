using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Services;
using Telegram.Bot;

namespace Domovoy.Notification.Service.Controllers;

[ApiController]
[Route("api/notifications")]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class NotificationSettingsController : ControllerBase
{
    private readonly NotificationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationSettingsController> _logger;

    public NotificationSettingsController(
        NotificationDbContext db,
        IConfiguration configuration,
        ILogger<NotificationSettingsController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Получить настройки уведомлений текущего пользователя
    /// </summary>
    [HttpGet("settings")]
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetUserId();

        var setting = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == "RuleTriggered");

        var channels = await _db.UserNotificationChannels
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync();

        var emailChannel = channels.FirstOrDefault(c => c.ChannelType.Equals("Email", StringComparison.OrdinalIgnoreCase));
        var tgChannel = channels.FirstOrDefault(c => c.ChannelType.Equals("Telegram", StringComparison.OrdinalIgnoreCase));

        var dto = new NotificationSettingsDto
        {
            EmailEnabled = setting?.EmailEnabled ?? (emailChannel != null),
            TelegramEnabled = setting?.TelegramEnabled ?? (tgChannel != null),
            RecipientEmail = setting?.RecipientEmail ?? emailChannel?.ChannelValue,
            TelegramChatId = setting?.TelegramChatId ?? tgChannel?.ChannelValue,
            TelegramBotToken = setting?.TelegramBotToken ?? _configuration["Telegram:BotToken"],
            SmtpHost = setting?.SmtpHost ?? _configuration["Smtp:Host"] ?? "smtp.mail.ru",
            SmtpPort = setting?.SmtpPort ?? (int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 465),
            SmtpUser = setting?.SmtpUser ?? _configuration["Smtp:User"],
            SmtpPass = setting?.SmtpPass,
            SmtpFromEmail = setting?.SmtpFromEmail ?? _configuration["Smtp:FromEmail"] ?? setting?.RecipientEmail
        };

        return Ok(dto);
    }

    /// <summary>
    /// Сохранить настройки уведомлений текущего пользователя
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] NotificationSettingsDto request)
    {
        var userId = GetUserId();

        // 1. Обновляем/создаем NotificationSetting для событий
        var eventTypes = new[] { "RuleTriggered", "CommandFailed" };
        foreach (var evt in eventTypes)
        {
            var setting = await _db.NotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == evt);

            if (setting == null)
            {
                setting = new NotificationSetting
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventType = evt,
                    EmailEnabled = request.EmailEnabled,
                    TelegramEnabled = request.TelegramEnabled,
                    RecipientEmail = request.RecipientEmail,
                    TelegramChatId = request.TelegramChatId,
                    TelegramBotToken = request.TelegramBotToken,
                    SmtpHost = request.SmtpHost,
                    SmtpPort = request.SmtpPort,
                    SmtpUser = request.SmtpUser,
                    SmtpPass = request.SmtpPass,
                    SmtpFromEmail = request.SmtpFromEmail,
                    CreatedAt = DateTime.UtcNow
                };
                _db.NotificationSettings.Add(setting);
            }
            else
            {
                setting.EmailEnabled = request.EmailEnabled;
                setting.TelegramEnabled = request.TelegramEnabled;
                setting.RecipientEmail = request.RecipientEmail;
                setting.TelegramChatId = request.TelegramChatId;
                setting.TelegramBotToken = request.TelegramBotToken;
                setting.SmtpHost = request.SmtpHost;
                setting.SmtpPort = request.SmtpPort;
                setting.SmtpUser = request.SmtpUser;
                if (!string.IsNullOrEmpty(request.SmtpPass))
                {
                    setting.SmtpPass = request.SmtpPass;
                }
                setting.SmtpFromEmail = request.SmtpFromEmail;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 2. Обновляем каналы
        var emailChannel = await _db.UserNotificationChannels
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ChannelType == "Email");

        if (!string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            if (emailChannel == null)
            {
                _db.UserNotificationChannels.Add(new UserNotificationChannel
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ChannelType = "Email",
                    ChannelValue = request.RecipientEmail.Trim(),
                    IsActive = request.EmailEnabled,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                emailChannel.ChannelValue = request.RecipientEmail.Trim();
                emailChannel.IsActive = request.EmailEnabled;
            }
        }

        var tgChannel = await _db.UserNotificationChannels
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ChannelType == "Telegram");

        if (!string.IsNullOrWhiteSpace(request.TelegramChatId))
        {
            if (tgChannel == null)
            {
                _db.UserNotificationChannels.Add(new UserNotificationChannel
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ChannelType = "Telegram",
                    ChannelValue = request.TelegramChatId.Trim(),
                    IsActive = request.TelegramEnabled,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                tgChannel.ChannelValue = request.TelegramChatId.Trim();
                tgChannel.IsActive = request.TelegramEnabled;
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Success] Notification settings saved for user {UserId}", userId);

        return Ok(request);
    }

    /// <summary>
    /// Тестовая отправка уведомления
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestNotification()
    {
        var userId = GetUserId();

        var setting = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == "RuleTriggered");

        var results = new List<string>();
        var errors = new List<string>();

        if (setting?.EmailEnabled == true && !string.IsNullOrWhiteSpace(setting.RecipientEmail))
        {
            try
            {
                var host = !string.IsNullOrWhiteSpace(setting.SmtpHost) ? setting.SmtpHost : (_configuration["Smtp:Host"] ?? "smtp.mail.ru");
                var port = setting.SmtpPort ?? (int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 465);
                var user = !string.IsNullOrWhiteSpace(setting.SmtpUser) ? setting.SmtpUser : (_configuration["Smtp:User"] ?? "");
                var pass = !string.IsNullOrWhiteSpace(setting.SmtpPass) ? setting.SmtpPass : (_configuration["Smtp:Pass"] ?? "");
                
                var from = setting.SmtpFromEmail;
                if (string.IsNullOrWhiteSpace(from) || from.EndsWith(".local") || !from.Contains("@"))
                {
                    from = !string.IsNullOrWhiteSpace(user) && user.Contains("@") ? user : setting.RecipientEmail;
                }

                _logger.LogInformation("Sending test email to {To} via {Host}:{Port} as {User} (from: {From})",
                    setting.RecipientEmail, host, port, user, from);

                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                {
                    throw new InvalidOperationException("SMTP Логин и Пароль приложения обязательны для отправки почты.");
                }

                var html = @"<h3>🏠 Система управления умным домом «Домовой»</h3>
<p>Это тестовое уведомление для проверки канала доставки Email.</p>
<p>✅ Почтовый сервер настроен корректно. Уведомления о срабатывании правил автоматизации будут приходить на этот адрес.</p>
<hr/><small>Время отправки: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC</small>";

                await EmailSender.SendEmailInternalAsync(host, port, user, pass, from, setting.RecipientEmail, "Тестовое уведомление системы «Домовой»", html);

                results.Add($"Email успешно доставлен на {setting.RecipientEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test email to {Email}", setting.RecipientEmail);
                errors.Add($"Email: {ex.Message}");
            }
        }

        if (setting?.TelegramEnabled == true && !string.IsNullOrWhiteSpace(setting.TelegramChatId))
        {
            try
            {
                var botToken = !string.IsNullOrWhiteSpace(setting.TelegramBotToken) ? setting.TelegramBotToken : _configuration["Telegram:BotToken"];
                if (string.IsNullOrEmpty(botToken))
                {
                    throw new InvalidOperationException("Telegram Bot Token не указан.");
                }

                var bot = new TelegramBotClient(botToken);
                await bot.SendMessage(
                    setting.TelegramChatId, 
                    "🏠 *Домовой*: Тестовое уведомление!\n\nКанал Telegram настроен корректно. Вы будете получать оповещения в этот чат.", 
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                results.Add($"Telegram успешно отправлен в {setting.TelegramChatId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test Telegram message to {ChatId}", setting.TelegramChatId);
                errors.Add($"Telegram: {ex.Message}");
            }
        }

        if (results.Count == 0 && errors.Count == 0)
        {
            return BadRequest(new { error = "Включите Email или Telegram в настройках и укажите адрес получателя / Chat ID." });
        }

        if (errors.Count > 0 && results.Count == 0)
        {
            _db.NotificationLogs.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "TestNotification",
                Channel = "Test",
                Message = "Ошибка отправки тестового уведомления",
                Status = "failed",
                ErrorMessage = string.Join("; ", errors),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return BadRequest(new { error = string.Join("\n", errors) });
        }

        _db.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "TestNotification",
            Channel = "Test",
            Message = "Тестовое уведомление системы Домовой",
            Status = "sent",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = string.Join("; ", results) });
    }

    /// <summary>
    /// История отправленных уведомлений
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int limit = 50)
    {
        var userId = GetUserId();

        var logs = await _db.NotificationLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(logs);
    }

    private string GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("Invalid user context");
        return userIdClaim;
    }
}