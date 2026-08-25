using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;
using Domovoy.RulesEngine.Service.Application.Mappers;
using Domovoy.Domain.Entities;
using Domovoy.Domain.Services;

namespace Domovoy.RulesEngine.Service.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// РџРµСЂРµРІРµРґРµРЅРѕ: РўСЂРµР±СѓРµС‚СЃСЏ User-JWT
[Authorize]  
public class RulesController : ControllerBase
{
    private readonly RulesEngineDbContext _db;
    private readonly ILogger<RulesController> _logger;
    private readonly IDomainRuleEngine _domainRuleEngine;

    public RulesController(
        RulesEngineDbContext db,
        ILogger<RulesController> logger,
        IDomainRuleEngine domainRuleEngine)
    {
        _db = db;
        _logger = logger;
        _domainRuleEngine = domainRuleEngine;
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РЎРїРёСЃРѕРє РїСЂР°РІРёР» С‚РµРєСѓС‰РµРіРѕ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules()
    {
        var userId = GetUserId();
        var rules = await _db.Rules
            .Where(r => r.UserId == userId.ToString())
            .Select(r => new RuleDto(
                r.Id, r.Name, r.DeviceId,
                r.SensorDeviceId,
                r.ActuatorDeviceId,
                r.Condition, r.Command, r.CommandParams,
                r.IsActive, r.Priority, r.CreatedAt))
            .ToListAsync();
        return Ok(rules);
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РЎРѕР·РґР°С‚СЊ РЅРѕРІРѕРµ РїСЂР°РІРёР»Рѕ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // РџРµСЂРµРІРµРґРµРЅРѕ: Р’Р°Р»РёРґР°С†РёСЏ СѓСЃР»РѕРІРёСЏ (РїСЂРѕРІРµСЂСЏРµРј СЃРёРЅС‚Р°РєСЃРёСЃ)
        if (!ValidateCondition(request.Condition))
            return BadRequest(new { error = "Invalid condition syntax" });

        var userId = GetUserId();

        // РџРµСЂРµРІРµРґРµРЅРѕ: 1. РЎРѕР·РґР°РЅРёРµ РґРѕРјРµРЅРЅРѕРіРѕ Р°РіСЂРµРіР°С‚Р° Scenario С‡РµСЂРµР· С„Р°Р±СЂРёС‡РЅС‹Р№ РјРµС‚РѕРґ Scenario.Create(...)
        var scenario = Scenario.Create(
            name: request.Name,
            userId: userId,
            isActive: true
        );

        // РџРµСЂРµРІРµРґРµРЅРѕ: 2. РќР°РїРѕР»РЅРµРЅРёРµ Р°РіСЂРµРіР°С‚Р° СѓСЃР»РѕРІРёСЏРјРё Рё РґРµР№СЃС‚РІРёСЏРјРё С‡РµСЂРµР· РјРµС‚РѕРґС‹ Р°РіСЂРµРіР°С‚Р°
        var parts = request.Condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            scenario.AddCondition(new ScenarioCondition
            {
                ConditionType = "Telemetry",
                Operator = parts[1],
                Value = parts[2],
                DeviceId = Guid.TryParse(request.DeviceId, out var did) ? did : null
            });
        }
        else
        {
            scenario.AddCondition(new ScenarioCondition
            {
                ConditionType = "TelemetryExpression",
                Operator = "==",
                Value = request.Condition
            });
        }

        scenario.AddAction(new ScenarioAction
        {
            ActionType = request.Command,
            Parameters = request.CommandParams ?? string.Empty
        });

        // РџРµСЂРµРІРµРґРµРЅРѕ: 3. Map to EF entity Rule for persistence
        var rule = scenario.ToRule(
            deviceId: request.ActuatorDeviceId ?? request.DeviceId,
            rawCondition: request.Condition,
            command: request.Command,
            commandParams: request.CommandParams,
            priority: request.Priority
        );

        rule.SensorDeviceId   = request.SensorDeviceId ?? request.DeviceId;
        rule.ActuatorDeviceId = request.ActuatorDeviceId ?? request.DeviceId;

        _db.Rules.Add(rule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("рџ“ќ Rule created: {RuleId} for {DeviceId}", rule.Id, rule.DeviceId);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id },
            new RuleDto(rule.Id, rule.Name, rule.DeviceId,
                rule.SensorDeviceId, rule.ActuatorDeviceId,
                rule.Condition, rule.Command, rule.CommandParams,
                rule.IsActive, rule.Priority, rule.CreatedAt));
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РџРѕР»СѓС‡РёС‚СЊ РїСЂР°РІРёР»Рѕ РїРѕ ID
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRule(Guid id)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();
        return Ok(new RuleDto(rule.Id, rule.Name, rule.DeviceId,
            rule.SensorDeviceId, rule.ActuatorDeviceId,
            rule.Condition, rule.Command, rule.CommandParams,
            rule.IsActive, rule.Priority, rule.CreatedAt));
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РћР±РЅРѕРІРёС‚СЊ РїСЂР°РІРёР»Рѕ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();

        // РџРµСЂРµРІРµРґРµРЅРѕ: Р’Р°Р»РёРґР°С†РёСЏ РїСЂРё РёР·РјРµРЅРµРЅРёРё СѓСЃР»РѕРІРёСЏ
        if (request.Condition != null && !ValidateCondition(request.Condition))
            return BadRequest(new { error = "Invalid condition syntax" });

        rule.Name = request.Name ?? rule.Name;
        rule.DeviceId = request.ActuatorDeviceId ?? request.DeviceId ?? rule.DeviceId;
        rule.SensorDeviceId   = request.SensorDeviceId   ?? rule.SensorDeviceId;
        rule.ActuatorDeviceId = request.ActuatorDeviceId ?? rule.ActuatorDeviceId;
        rule.Condition = request.Condition ?? rule.Condition;
        rule.Command = request.Command ?? rule.Command;
        rule.CommandParams = request.CommandParams ?? rule.CommandParams;
        rule.Priority = request.Priority ?? rule.Priority;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: РЈРґР°Р»РёС‚СЊ РїСЂР°РІРёР»Рѕ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();

        _db.Rules.Remove(rule);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: Р’РєР»СЋС‡РёС‚СЊ/РІС‹РєР»СЋС‡РёС‚СЊ РїСЂР°РІРёР»Рѕ
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    [HttpPost("{id}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleRule(Guid id)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();

        // РџРµСЂРµРІРµРґРµРЅРѕ: РР·РјРµРЅРµРЅРёРµ СЃРѕСЃС‚РѕСЏРЅРёСЏ С‡РµСЂРµР· РјРµС‚РѕРґ РґРѕРјРµРЅРЅРѕРіРѕ Р°РіСЂРµРіР°С‚Р° Scenario
        var scenario = rule.ToDomainScenario();
        scenario.Toggle();

        rule.IsActive = scenario.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("рџ”„ Rule {RuleId} toggled: IsActive={IsActive}",
            rule.Id, rule.IsActive);
        return NoContent();
    }

    // РџРµСЂРµРІРµРґРµРЅРѕ: Р’Р°Р»РёРґР°С†РёСЏ СѓСЃР»РѕРІРёСЏ С‡РµСЂРµР· РґРѕРјРµРЅРЅС‹Р№ СЃРµСЂРІРёСЃ (С‚РѕР»СЊРєРѕ СЃРёРЅС‚Р°РєСЃРёСЃ)
    private bool ValidateCondition(string condition)
    {
        return _domainRuleEngine.ValidateExpression(condition);
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

// РџРµСЂРµРІРµРґРµРЅРѕ: DTOs
public record RuleDto(
    Guid Id,
    string Name,
    string DeviceId,
    string? SensorDeviceId,
    string? ActuatorDeviceId,
    string Condition,
    string Command,
    string? CommandParams,
    bool IsActive,
    int Priority,
    DateTime CreatedAt);

public record CreateRuleRequest(
    [Required, MaxLength(100)] string Name,
    [Required] string DeviceId,
    string? SensorDeviceId,
    string? ActuatorDeviceId,

    // РџРµСЂРµРІРµРґРµРЅРѕ: "temperature > 25"
    [Required] string Condition,
    
    // РџРµСЂРµРІРµРґРµРЅРѕ: "turn_on"
    [Required] string Command,

    // РџРµСЂРµРІРµРґРµРЅРѕ: JSON
    string? CommandParams,        
    int Priority = 0);

public record UpdateRuleRequest(
    string? Name,
    string? DeviceId,
    string? SensorDeviceId,
    string? ActuatorDeviceId,
    string? Condition,
    string? Command,
    string? CommandParams,
    int? Priority);
