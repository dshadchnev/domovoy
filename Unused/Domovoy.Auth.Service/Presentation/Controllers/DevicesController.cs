using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Auth.Service.Presentation.Contracts;
using Domovoy.Auth.Service.Application.Services;
using Domovoy.Auth.Service.Presentation.Workers;
using System.Text.Json;
using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Shared.Attributes;

namespace Domovoy.Auth.Service.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly ILogger<DevicesController> _logger;
    private readonly IPublishEndpoint _bus;
    private readonly Domovoy.Auth.Service.Infrastructure.Persistence.AuthDbContext _db;

    public DevicesController(
        IDeviceAuthService deviceAuthService,
        ILogger<DevicesController> logger,
        IPublishEndpoint bus,
        Domovoy.Auth.Service.Infrastructure.Persistence.AuthDbContext db)
    {
        _deviceAuthService = deviceAuthService;
        _logger = logger;
        _bus = bus;
        _db = db;
    }

    /// <summary>
    /// Р РµРіРёСЃС‚СЂР°С†РёСЏ РЅРѕРІРѕРіРѕ СѓСЃС‚СЂРѕР№СЃС‚РІР°
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceCredentialResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _deviceAuthService.RegisterAsync(request, userId, GetClientIp());
            return Created(string.Empty, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("РћС€РёР±РєР° СЂРµРіРёСЃС‚СЂР°С†РёРё: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// РћС‚Р·С‹РІ СѓСЃС‚СЂРѕР№СЃС‚РІР° вЂ” Р±Р»РѕРєРёСЂСѓРµС‚ Р°СѓС‚РµРЅС‚РёС„РёРєР°С†РёСЋ
    /// </summary>
    [HttpPost("{deviceId}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeDevice(string deviceId)
    {
        try
        {
            var userId = GetUserId();
            await _deviceAuthService.RevokeDeviceAsync(deviceId, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Р РѕС‚Р°С†РёСЏ СЃРµРєСЂРµС‚Р° вЂ” РєСЂРёС‚РёС‡РЅР°СЏ РѕРїРµСЂР°С†РёСЏ Р±РµР·РѕРїР°СЃРЅРѕСЃС‚Рё
    /// </summary>
    [HttpPost("{deviceId}/rotate-secret")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateSecret(string deviceId)
    {
        try
        {
            var userId = GetUserId();
            await _deviceAuthService.RotateSecretAsync(deviceId, userId);
            return Ok(new { message = "Secret rotated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Прием телеметрии от авторизованного устройства
    /// </summary>
    [HttpPost("{id}/telemetry")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [RequireDeviceOwnership("id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveTelemetry(string id, [FromBody] JsonElement telemetry)
    {
        _logger.LogInformation("📡 Telemetry received from {DeviceId}", id);

        // Публикация в шину через Transactional Outbox
        await _bus.Publish(new TelemetryReceivedEvent(id, telemetry.GetRawText(), DateTime.UtcNow));
        await _db.SaveChangesAsync();

        return Ok(new { status = "accepted", timestamp = DateTime.UtcNow });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user context");
        return userId;
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}