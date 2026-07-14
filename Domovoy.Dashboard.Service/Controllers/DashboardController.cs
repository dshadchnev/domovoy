using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Dashboard.Service.Data;
using Domovoy.Dashboard.Service.Services;

namespace Domovoy.Dashboard.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _repository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardRepository repository,
        ILogger<DashboardController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Сводная информация по системе
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetUserId();
        var summary = await _repository.GetSummaryAsync(userId);
        return Ok(summary);
    }

    /// <summary>
    /// Список устройств со статусами
    /// </summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<DeviceStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices()
    {
        var userId = GetUserId();
        var devices = await _repository.GetDevicesAsync(userId);
        return Ok(devices);
    }

    /// <summary>
    /// История телеметрии устройства
    /// </summary>
    [HttpGet("telemetry/{deviceId}")]
    [ProducesResponseType(typeof(TelemetryHistoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTelemetry(
        string deviceId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromTime = from ?? DateTime.UtcNow.AddHours(-24);
        var toTime = to ?? DateTime.UtcNow;

        var history = await _repository.GetTelemetryHistoryAsync(deviceId, fromTime, toTime);
        return Ok(history);
    }

    /// <summary>
    /// Статистика команд
    /// </summary>
    [HttpGet("commands/stats")]
    [ProducesResponseType(typeof(CommandStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommandStats(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = GetUserId();
        var stats = await _repository.GetCommandStatsAsync(userId, from, to);
        return Ok(stats);
    }

    /// <summary>
    /// Последние команды
    /// </summary>
    [HttpGet("commands/recent")]
    [ProducesResponseType(typeof(List<CommandLog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentCommands([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var commands = await _repository.GetRecentCommandsAsync(userId, limit);
        return Ok(commands);
    }

    /// <summary>
    /// Активные правила
    /// </summary>
    [HttpGet("rules/active")]
    [ProducesResponseType(typeof(List<Rule>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveRules()
    {
        var userId = GetUserId();
        var rules = await _repository.GetActiveRulesAsync(userId);
        return Ok(rules);
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