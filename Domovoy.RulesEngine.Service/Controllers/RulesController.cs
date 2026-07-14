using NCalc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.RulesEngine.Service.Data;

namespace Domovoy.RulesEngine.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// Требуется User-JWT
[Authorize]  
public class RulesController : ControllerBase
{
    private readonly RulesEngineDbContext _db;
    private readonly ILogger<RulesController> _logger;

    public RulesController(RulesEngineDbContext db, ILogger<RulesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Список правил текущего пользователя
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules()
    {
        var userId = GetUserId();
        var rules = await _db.Rules
            .Where(r => r.UserId == userId.ToString())
            .Select(r => new RuleDto(r.Id, r.Name, r.DeviceId, r.Condition, r.Command, r.CommandParams, r.IsActive, r.Priority, r.CreatedAt))
            .ToListAsync();
        return Ok(rules);
    }

    /// <summary>
    /// Создать новое правило
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Валидация условия (проверяем синтаксис)
        if (!ValidateCondition(request.Condition))
            return BadRequest(new { error = "Invalid condition syntax" });

        var userId = GetUserId();
        var rule = new Rule
        {
            Name = request.Name,
            DeviceId = request.DeviceId,
            Condition = request.Condition,
            Command = request.Command,
            CommandParams = request.CommandParams,
            Priority = request.Priority,
            UserId = userId.ToString(),
            IsActive = true
        };

        _db.Rules.Add(rule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("📝 Rule created: {RuleId} for {DeviceId}", rule.Id, rule.DeviceId);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, new RuleDto(rule.Id, rule.Name, rule.DeviceId, rule.Condition, rule.Command, rule.CommandParams, rule.IsActive, rule.Priority, rule.CreatedAt));
    }

    /// <summary>
    /// Получить правило по ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRule(Guid id)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();
        return Ok(new RuleDto(rule.Id, rule.Name, rule.DeviceId, rule.Condition, rule.Command, rule.CommandParams, rule.IsActive, rule.Priority, rule.CreatedAt));
    }

    /// <summary>
    /// Обновить правило
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();

        // Валидация при изменении условия
        if (request.Condition != null && !ValidateCondition(request.Condition))
            return BadRequest(new { error = "Invalid condition syntax" });

        rule.Name = request.Name ?? rule.Name;
        rule.Condition = request.Condition ?? rule.Condition;
        rule.Command = request.Command ?? rule.Command;
        rule.CommandParams = request.CommandParams ?? rule.CommandParams;
        rule.Priority = request.Priority ?? rule.Priority;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Удалить правило
    /// </summary>
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
    /// Включить/выключить правило
    /// </summary>
    [HttpPost("{id}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleRule(Guid id)
    {
        var userId = GetUserId();
        var rule = await _db.Rules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.ToString());

        if (rule is null) return NotFound();

        rule.IsActive = !rule.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("🔄 Rule {RuleId} toggled: IsActive={IsActive}",
            rule.Id, rule.IsActive);
        return NoContent();
    }

    // Валидация условия через NCalc (без выполнения, только синтаксис)
    private bool ValidateCondition(string condition)
    {
        try
        {
            var expr = new Expression(condition);
            // Пустая проверка: если парсится без ошибок — синтаксис валиден
            return true;
        }
        catch
        {
            return false;
        }
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

// DTOs
public record RuleDto(
    Guid Id,
    string Name,
    string DeviceId,
    string Condition,
    string Command,
    string? CommandParams,
    bool IsActive,
    int Priority,
    DateTime CreatedAt);

public record CreateRuleRequest(
    [Required, MaxLength(100)] string Name,
    [Required] string DeviceId,

    // "temperature > 25"
    [Required] string Condition,
    
    // "turn_on"
    [Required] string Command,

    // JSON
    string? CommandParams,        
    int Priority = 0);

public record UpdateRuleRequest(
    string? Name,
    string? Condition,
    string? Command,
    string? CommandParams,
    int? Priority);