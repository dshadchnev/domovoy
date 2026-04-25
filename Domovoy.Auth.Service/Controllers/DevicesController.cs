using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Services;
using OpenIddict.Abstractions;


namespace Domovoy.Auth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DevicesController : ControllerBase
{
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceAuthService deviceAuthService, ILogger<DevicesController> logger)
    {
        _deviceAuthService = deviceAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Регистрация нового устройства для текущего пользователя
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceCredentialResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _deviceAuthService.RegisterAsync(request, userId, GetClientIp());
            return CreatedAtAction(nameof(RegisterDevice), new { deviceId = request.NetworkDeviceId }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Ошибка регистрации устройства: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при регистрации устройства");
            return StatusCode(500, new { error = "An error occurred during device registration" });
        }
    }

    /// <summary>
    /// Отзыв устройства (блокировка доступа)
    /// </summary>
    [HttpPost("{deviceId}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
            _logger.LogWarning("Устройство не найдено: {DeviceId}", deviceId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отзыве устройства");
            return StatusCode(500, new { error = "An error occurred during device revocation" });
        }
    }

    /// <summary>
    /// Ротация секрета устройства (обновить пароль)
    /// </summary>
    [HttpPost("{deviceId}/rotate-secret")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
            _logger.LogWarning("Устройство не найдено: {DeviceId}", deviceId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при ротации секрета");
            return StatusCode(500, new { error = "An error occurred during secret rotation" });
        }
    }

    private Guid GetUserId()
    {
        // OpenIddict использует ClaimTypes.NameIdentifier по умолчанию,
        // но также может использовать OpenIddictConstants.Claims.Subject
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user context");
        return userId;
    }

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
