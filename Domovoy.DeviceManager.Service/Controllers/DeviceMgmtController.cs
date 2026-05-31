using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.DeviceManager.Service.Data;
using Domovoy.DeviceManager.Service.Events;
using MassTransit;

namespace Domovoy.DeviceManager.Service.Controllers;

[ApiController]
[Route("api/device-mgmt")]
[Produces("application/json")]
[Authorize]
public class DeviceMgmtController : ControllerBase
{
    private readonly DeviceManagerDbContext _db;
    private readonly ILogger<DeviceMgmtController> _logger;
    private readonly IPublishEndpoint _bus;

    public DeviceMgmtController(
        DeviceManagerDbContext db,
        ILogger<DeviceMgmtController> logger,
        IPublishEndpoint bus)
    {
        _db = db;
        _logger = logger;
        _bus = bus;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices()
    {
        var userId = GetUserId();

        var devices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId && !d.IsRevoked)
            .Select(d => new DeviceDto(
                d.NetworkDeviceId,
                d.Name,
                d.RoomId.HasValue ? d.RoomId.Value.ToString() : null,
                d.IsRevoked,
                d.CreatedAt))
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDevice(string id)
    {
        var userId = GetUserId();

        var device = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == id && d.OwnerUserId == userId);

        if (device is null) return NotFound();

        return Ok(new DeviceDto(
            device.NetworkDeviceId,
            device.Name,
            device.RoomId.HasValue ? device.RoomId.Value.ToString() : null,
            device.IsRevoked,
            device.CreatedAt));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDevice(string id, [FromBody] UpdateDeviceRequest request)
    {
        var userId = GetUserId();

        var device = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == id && d.OwnerUserId == userId);

        if (device is null) return NotFound();

        Guid? newRoomId = Guid.TryParse(request.RoomId, out var rid) ? rid : null;
        var isChanged = device.Name != request.Name || device.RoomId != newRoomId;

        device.Name = request.Name;
        device.RoomId = newRoomId;
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (isChanged)
        {
            await _bus.Publish(new DeviceUpdatedEvent(
                device.NetworkDeviceId,
                device.Name,
                device.RoomId.HasValue ? device.RoomId.Value.ToString() : null,
                device.IsRevoked,
                device.UpdatedAt.Value));
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDevice(string id)
    {
        var userId = GetUserId();

        var device = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == id && d.OwnerUserId == userId);

        if (device is null) return NotFound();

        _db.DeviceCredentials.Remove(device);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("Invalid user context");

        return userId;
    }
}

public record DeviceDto(string NetworkDeviceId, string? Name, string? RoomId, bool IsRevoked, DateTime CreatedAt);
public record UpdateDeviceRequest(string? Name, string? RoomId);