using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.Notification.Service.Data;

namespace Domovoy.Notification.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// Получить настройки уведомлений текущего пользователя
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetUserId();

        var settings = await _db.NotificationSettings
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var channels = await _db.UserNotificationChannels
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync();

        return Ok(new
        {
            settings,
            channels
        });
    }

    /// <summary>
    /// Обновить настройки для типа события
    /// </summary>
    [HttpPut("settings/{eventType}")]
    public async Task<IActionResult> UpdateSettings(
        string eventType,
        [FromBody] UpdateSettingsRequest request)
    {
        var userId = GetUserId();

        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == eventType);

        if (settings == null)
        {
            settings = new NotificationSetting
            {
                UserId = userId,
                EventType = eventType
            };
            _db.NotificationSettings.Add(settings);
        }

        settings.TelegramEnabled = request.TelegramEnabled;
        settings.EmailEnabled = request.EmailEnabled;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(settings);
    }

    /// <summary>
    /// Добавить канал уведомлений
    /// </summary>
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
    /// Удалить канал уведомлений
    /// </summary>
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

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user context");
        return userId;
    }
}

public record UpdateSettingsRequest(
    bool TelegramEnabled,
    bool EmailEnabled);

public record AddChannelRequest(
    string ChannelType,
    string ChannelValue);