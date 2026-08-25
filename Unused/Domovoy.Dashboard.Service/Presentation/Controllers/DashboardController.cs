using Domovoy.Dashboard.Service.Presentation.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Dashboard.Service.Infrastructure.Persistence;
using Domovoy.Dashboard.Service.Application.Services;

namespace Domovoy.Dashboard.Service.Presentation.Controllers;

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
/// Переведено: Р РЋР Р†Р С•Р Т‘Р Р…Р В°РЎРЏ Р С‘Р Р…РЎвЂћР С•РЎР‚Р СР В°РЎвЂ Р С‘РЎРЏ Р С—Р С• РЎРѓР С‘РЎРѓРЎвЂљР ВµР СР Вµ
/// Переведено: </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetUserId();
        var summary = await _repository.GetSummaryAsync(userId);
        return Ok(summary);
    }

    /// <summary>
/// Переведено: Р РЋР С—Р С‘РЎРѓР С•Р С” РЎС“РЎРѓРЎвЂљРЎР‚Р С•Р в„–РЎРѓРЎвЂљР Р† РЎРѓР С• РЎРѓРЎвЂљР В°РЎвЂљРЎС“РЎРѓР В°Р СР С‘
/// Переведено: </summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<DeviceStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices()
    {
        var userId = GetUserId();
        var devices = await _repository.GetDevicesAsync(userId);
        return Ok(devices);
    }

    /// <summary>
/// Переведено: Р ВРЎРѓРЎвЂљР С•РЎР‚Р С‘РЎРЏ РЎвЂљР ВµР В»Р ВµР СР ВµРЎвЂљРЎР‚Р С‘Р С‘ РЎС“РЎРѓРЎвЂљРЎР‚Р С•Р в„–РЎРѓРЎвЂљР Р†Р В°
/// Переведено: </summary>
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
/// Переведено: Р РЋРЎвЂљР В°РЎвЂљР С‘РЎРѓРЎвЂљР С‘Р С”Р В° Р С”Р С•Р СР В°Р Р…Р Т‘
/// Переведено: </summary>
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
/// Переведено: Р СџР С•РЎРѓР В»Р ВµР Т‘Р Р…Р С‘Р Вµ Р С”Р С•Р СР В°Р Р…Р Т‘РЎвЂ№
/// Переведено: </summary>
    [HttpGet("commands/recent")]
    [ProducesResponseType(typeof(List<CommandLog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentCommands([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var commands = await _repository.GetRecentCommandsAsync(userId, limit);
        return Ok(commands);
    }

    /// <summary>
/// Переведено: Р С’Р С”РЎвЂљР С‘Р Р†Р Р…РЎвЂ№Р Вµ Р С—РЎР‚Р В°Р Р†Р С‘Р В»Р В°
/// Переведено: </summary>
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