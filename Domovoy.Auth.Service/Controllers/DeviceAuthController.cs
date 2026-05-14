using Microsoft.AspNetCore.Mvc;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Services;

namespace Domovoy.Auth.Service.Controllers;

[ApiController]
[Route("api/device-auth")] // было [Route("api/[controller]")]
[Produces("application/json")]
public class DeviceAuthController : ControllerBase
{
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly ILogger<DeviceAuthController> _logger;

    public DeviceAuthController(IDeviceAuthService deviceAuthService, ILogger<DeviceAuthController> logger)
    {
        _deviceAuthService = deviceAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Аутентификация устройства (получить JWT для устройства)
    /// </summary>
    /// <remarks>
    /// Используется для аутентификации IoT устройств.
    /// Возвращает JWT токен для использования в заголовке Authorization
    /// </remarks>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(DeviceTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Authenticate([FromBody] DeviceAuthRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _deviceAuthService.AuthenticateAsync(request, GetClientIp());
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Неудачная аутентификация устройства: {Message}", ex.Message);
            return Unauthorized(new { error = "Invalid device credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при аутентификации устройства");
            return StatusCode(500, new { error = "An error occurred during device authentication" });
        }
    }

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
