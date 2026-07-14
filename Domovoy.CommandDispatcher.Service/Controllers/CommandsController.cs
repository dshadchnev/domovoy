using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.CommandDispatcher.Service.Data;
using MassTransit;
using Domovoy.Shared.Events;

namespace Domovoy.CommandDispatcher.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class CommandsController : ControllerBase
{
    private readonly DispatcherDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CommandsController> _logger;

    public CommandsController(DispatcherDbContext db, IPublishEndpoint publishEndpoint, ILogger<CommandsController> logger)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Список команд устройств текущего пользователя
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CommandLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommands(
        [FromQuery] string? deviceId,
        [FromQuery] string? status,
        [FromQuery] int limit = 50)
    {
        var userId = GetUserId();

        // Получаем список устройств пользователя
        var userDevices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId)
            .Select(d => d.NetworkDeviceId)
            .ToListAsync();

        var query = _db.CommandLogs
            .Where(c => userDevices.Contains(c.DeviceId));

        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(c => c.DeviceId == deviceId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        var commands = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new CommandLogDto(
                c.Id, c.DeviceId, c.Command, c.Params,
                c.SourceRuleId, c.Status, c.ErrorMessage,
                c.Protocol, c.Endpoint, c.CreatedAt, c.SentAt, c.CompletedAt))
            .ToListAsync();

        return Ok(commands);
    }

    /// <summary>
    /// Детали отдельной команды
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CommandLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCommand(Guid id)
    {
        var userId = GetUserId();

        var command = await _db.CommandLogs
            .FirstOrDefaultAsync(c => c.Id == id);

        if (command == null) return NotFound();

        // Проверка прав доступа: команда должна быть для устройства пользователя
        var hasAccess = await _db.DeviceCredentials
            .AnyAsync(d => d.NetworkDeviceId == command.DeviceId && d.OwnerUserId == userId);

        if (!hasAccess) return NotFound(); 

        return Ok(new CommandLogDto(
            command.Id, command.DeviceId, command.Command, command.Params,
            command.SourceRuleId, command.Status, command.ErrorMessage,
            command.Protocol, command.Endpoint, command.CreatedAt, command.SentAt, command.CompletedAt));
    }

    /// <summary>
    /// Повторная отправка неудавшейся команды
    /// </summary>
    [HttpPost("{id}/retry")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryCommand(Guid id)
    {
        var userId = GetUserId();

        var command = await _db.CommandLogs.FirstOrDefaultAsync(c => c.Id == id);
        if (command == null) return NotFound();

        // Проверка прав
        var hasAccess = await _db.DeviceCredentials
            .AnyAsync(d => d.NetworkDeviceId == command.DeviceId && d.OwnerUserId == userId);
        if (!hasAccess) return NotFound();

        if (command.Status != "failed")
            return BadRequest(new { error = "Can only retry failed commands" });

        // Сбрасываем статус в pending для повторной обработки
        command.Status = "pending";
        command.ErrorMessage = null;
        command.SentAt = null;
        await _db.SaveChangesAsync();

        // Публикуем событие повтора
        await _publishEndpoint.Publish(new ExecuteCommandEvent(
            command.DeviceId,
            command.Command,
            command.Params,
            command.SourceRuleId,
            DateTime.UtcNow));

        _logger.LogInformation("🔄 Command {CommandId} queued for retry", id);
        return Accepted();
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

// DTO для ответа
public record CommandLogDto(
    Guid Id,
    string DeviceId,
    string Command,
    string? Params,
    string? SourceRuleId,
    string Status,
    string? ErrorMessage,
    string? Protocol,
    string? Endpoint,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? CompletedAt);