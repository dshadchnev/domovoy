using Microsoft.AspNetCore.Mvc;
using Domovoy.Auth.Service.Presentation.Contracts;
using Domovoy.Auth.Service.Application.Services;
using Domovoy.Auth.Service.Presentation.Workers;

namespace Domovoy.Auth.Service.Presentation.Controllers;

[ApiController]
[Route("api/device-auth")] 
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
    /// РђСѓС‚РµРЅС‚РёС„РёРєР°С†РёСЏ СѓСЃС‚СЂРѕР№СЃС‚РІР° (РїРѕР»СѓС‡РёС‚СЊ JWT РґР»СЏ СѓСЃС‚СЂРѕР№СЃС‚РІР°)
    /// </summary>
    /// <remarks>
    /// РСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РґР»СЏ Р°СѓС‚РµРЅС‚РёС„РёРєР°С†РёРё IoT СѓСЃС‚СЂРѕР№СЃС‚РІ.
    /// Р’РѕР·РІСЂР°С‰Р°РµС‚ JWT С‚РѕРєРµРЅ РґР»СЏ РёСЃРїРѕР»СЊР·РѕРІР°РЅРёСЏ РІ Р·Р°РіРѕР»РѕРІРєРµ Authorization
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
            _logger.LogWarning("РќРµСѓРґР°С‡РЅР°СЏ Р°СѓС‚РµРЅС‚РёС„РёРєР°С†РёСЏ СѓСЃС‚СЂРѕР№СЃС‚РІР°: {Message}", ex.Message);
            return Unauthorized(new { error = "Invalid device credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "РћС€РёР±РєР° РїСЂРё Р°СѓС‚РµРЅС‚РёС„РёРєР°С†РёРё СѓСЃС‚СЂРѕР№СЃС‚РІР°");
            return StatusCode(500, new { error = "An error occurred during device authentication" });
        }
    }

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
