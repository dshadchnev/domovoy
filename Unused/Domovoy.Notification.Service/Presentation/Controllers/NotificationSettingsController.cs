using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.Notification.Service.Infrastructure.Persistence;

namespace Domovoy.Notification.Service.Presentation.Controllers;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
[Authorize]
public class NotificationSettingsController : ControllerBase
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<NotificationSettingsController> _logger;

    public NotificationSettingsController(
        NotificationDbContext db,
        ILogger<NotificationSettingsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РџРѕР»СѓС‡РёС‚СЊ РЅР°СЃС‚СЂРѕР№РєРё СѓРІРµРґРѕРјР»РµРЅРёР№ С‚РµРєСѓС‰РµРіРѕ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetUserId();

        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            return Ok(new NotificationSettingsDto(
                EmailEnabled: true,
                TelegramEnabled: false,
                TelegramBotToken: "",
                TelegramChatId: "",
                SmtpHost: "smtp.gmail.com",
                SmtpPort: 587,
                SmtpUser: "",
                SmtpPass: "",
                SmtpFromEmail: "noreply@domovoy.local",
                RecipientEmail: ""
            ));
        }

        return Ok(new NotificationSettingsDto(
            EmailEnabled: settings.EmailEnabled,
            TelegramEnabled: settings.TelegramEnabled,
            TelegramBotToken: settings.TelegramBotToken ?? "",
            TelegramChatId: settings.TelegramChatId ?? "",
            SmtpHost: settings.SmtpHost ?? "smtp.gmail.com",
            SmtpPort: settings.SmtpPort ?? 587,
            SmtpUser: settings.SmtpUser ?? "",
            SmtpPass: settings.SmtpPass ?? "",
            SmtpFromEmail: !string.IsNullOrWhiteSpace(settings.SmtpFromEmail) ? settings.SmtpFromEmail : "noreply@domovoy.local",
            RecipientEmail: settings.RecipientEmail ?? ""
        ));
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РћР±РЅРѕРІРёС‚СЊ РЅР°СЃС‚СЂРѕР№РєРё
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] NotificationSettingsDto request)
    {
        var userId = GetUserId();

        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            settings = new NotificationSetting
            {
                UserId = userId,
                EventType = "All"
            };
            _db.NotificationSettings.Add(settings);
        }

        settings.TelegramEnabled = request.TelegramEnabled;
        settings.EmailEnabled = request.EmailEnabled;
        settings.TelegramBotToken = request.TelegramBotToken;
        settings.TelegramChatId = request.TelegramChatId;
        settings.SmtpHost = request.SmtpHost;
        settings.SmtpPort = request.SmtpPort;
        settings.SmtpUser = request.SmtpUser;
        settings.SmtpPass = request.SmtpPass;
        settings.SmtpFromEmail = request.SmtpFromEmail;
        settings.RecipientEmail = request.RecipientEmail;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(settings);
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: Р”РѕР±Р°РІРёС‚СЊ РєР°РЅР°Р» СѓРІРµРґРѕРјР»РµРЅРёР№
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPost("channels")]
    public async Task<IActionResult> AddChannel([FromBody] AddChannelRequest request)
    {
        var userId = GetUserId();

        var channel = new UserNotificationChannel
        {
            UserId = userId,
            ChannelType = request.ChannelType,
            ChannelValue = request.ChannelValue
        };

        _db.UserNotificationChannels.Add(channel);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSettings), new { id = channel.Id }, channel);
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РЈРґР°Р»РёС‚СЊ РєР°РЅР°Р» СѓРІРµРґРѕРјР»РµРЅРёР№
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpDelete("channels/{id}")]
    public async Task<IActionResult> DeleteChannel(Guid id)
    {
        var userId = GetUserId();

        var channel = await _db.UserNotificationChannels
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (channel == null) return NotFound();

        channel.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РСЃС‚РѕСЂРёСЏ РѕС‚РїСЂР°РІР»РµРЅРЅС‹С… СѓРІРµРґРѕРјР»РµРЅРёР№
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
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

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РўРµСЃС‚РѕРІР°СЏ РѕС‚РїСЂР°РІРєР° СѓРІРµРґРѕРјР»РµРЅРёР№
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestNotification(
        [FromServices] IEnumerable<Domovoy.Notification.Service.Infrastructure.External.INotificationSender> senders)
    {
        var userId = GetUserId();
        var settings = await _db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
            return BadRequest("РќР°СЃС‚СЂРѕР№РєРё СѓРІРµРґРѕРјР»РµРЅРёР№ РЅРµ СЃРѕС…СЂР°РЅРµРЅС‹");

        var errors = new List<string>();
        var sentChannels = new List<string>();

        var subject = "рџ§Є РўРµСЃС‚РѕРІРѕРµ СѓРІРµРґРѕРјР»РµРЅРёРµ Domovoy";
        var message = "РџРѕР·РґСЂР°РІР»СЏРµРј! РќР°СЃС‚СЂРѕР№РєРё РєР°РЅР°Р»РѕРІ СѓРІРµРґРѕРјР»РµРЅРёР№ СЃРёСЃС‚РµРјС‹ СѓРјРЅРѕРіРѕ РґРѕРјР° В«Р”РѕРјРѕРІРѕР№В» СѓСЃРїРµС€РЅРѕ СЃРѕРµРґРёРЅРµРЅС‹ Рё СЂР°Р±РѕС‚Р°СЋС‚.";

        if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            try
            {
                var tgSender = senders.OfType<Domovoy.Notification.Service.Infrastructure.External.TelegramSender>().FirstOrDefault();
                if (tgSender != null)
                {
                    await tgSender.SendCustomAsync(settings.TelegramBotToken, settings.TelegramChatId, subject, message);
                    sentChannels.Add("Telegram");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Telegram: {ex.Message}");
            }
        }

        if (settings.EmailEnabled && !string.IsNullOrWhiteSpace(settings.RecipientEmail))
        {
            try
            {
                var emailSender = senders.OfType<Domovoy.Notification.Service.Infrastructure.External.EmailSender>().FirstOrDefault();
                if (emailSender != null)
                {
                    await emailSender.SendCustomAsync(
                        settings.SmtpHost, settings.SmtpPort, settings.SmtpUser,
                        settings.SmtpPass, settings.SmtpFromEmail, settings.RecipientEmail,
                        subject, message);
                    sentChannels.Add("Email");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Email: {ex.Message}");
            }
        }

        if (sentChannels.Count == 0 && errors.Count == 0)
        {
            return BadRequest("РќРё РѕРґРёРЅ РєР°РЅР°Р» РѕС‚РїСЂР°РІРєРё РЅРµ РІРєР»СЋС‡РµРЅ РёР»Рё РЅРµ Р·Р°РїРѕР»РЅРµРЅС‹ РїРѕР»СѓС‡Р°С‚РµР»Рё.");
        }

        if (errors.Count > 0)
        {
            return StatusCode(500, new { message = "РћС€РёР±РєРё РїСЂРё РѕС‚РїСЂР°РІРєРµ", errors, sentChannels });
        }

        return Ok(new { message = $"РўРµСЃС‚РѕРІРѕРµ СѓРІРµРґРѕРјР»РµРЅРёРµ СѓСЃРїРµС€РЅРѕ РѕС‚РїСЂР°РІР»РµРЅРѕ: {string.Join(", ", sentChannels)}" });
    }

    private string GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("nameid")?.Value
                       ?? User.Identity?.Name
                       ?? "default-user";
        return userIdClaim;
    }
}

public record NotificationSettingsDto(
    bool EmailEnabled,
    bool TelegramEnabled,
    string? TelegramBotToken,
    string? TelegramChatId,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUser,
    string? SmtpPass,
    string? SmtpFromEmail,
    string? RecipientEmail);

public record AddChannelRequest(
    string ChannelType,
    string ChannelValue);
