// Domovoy.Auth.Service/Controllers/DevicesController.cs
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Services;
using System.Text.Json;
using MassTransit;
using Domovoy.Shared.Events;

namespace Domovoy.Auth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly ILogger<DevicesController> _logger;
    private readonly IPublishEndpoint _bus;

    public DevicesController(IDeviceAuthService deviceAuthService, ILogger<DevicesController> logger, IPublishEndpoint bus)
    {
        _deviceAuthService = deviceAuthService;
        _logger = logger;
        _bus = bus;
    }

    /// <summary>
    /// Регистрация нового устройства (ГЕНЕРАЦИЯ СЕКРЕТА) — только здесь!
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
            _logger.LogWarning("Ошибка регистрации: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отзыв устройства — блокирует аутентификацию
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
    /// Ротация секрета — критичная операция безопасности
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
    /// ПРИЁМ ТЕЛЕМЕТРИИ — критичная проверка безопасности
    /// </summary>
    [HttpPost("{id}/telemetry")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveTelemetry(string id, [FromBody] JsonElement telemetry)
    {
        // 🔐 КЛЮЧЕВАЯ ПРОВЕРКА: DeviceId claim == URL ID
        var tokenDeviceId = User.FindFirstValue("DeviceId")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (tokenDeviceId != id)
        {
            _logger.LogWarning("⚠️ Device ID mismatch. URL: {UrlId}, Token: {TokenId}", id, tokenDeviceId);
            return Forbid();
        }

        _logger.LogInformation("📡 Telemetry received from {DeviceId}", id);

        // Публикация в шину (тот же контракт для всех устройств)
        await _bus.Publish(new TelemetryReceivedEvent(id, telemetry.GetRawText(), DateTime.UtcNow));

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