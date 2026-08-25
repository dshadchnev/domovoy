using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Domovoy.DeviceManager.Service.Infrastructure.Persistence;
using Domovoy.DeviceManager.Service.Application.Mappers;
using Domovoy.Domain.Entities;
using Domovoy.Shared.Events;
using MassTransit;

namespace Domovoy.DeviceManager.Service.Presentation.Controllers;

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
                d.Protocol,      
                d.Endpoint,      
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
            device.Protocol,      
            device.Endpoint,      
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

        var isChanged = device.Name != request.Name
                     || device.RoomId != newRoomId
                     || device.Protocol != request.Protocol
                     || device.Endpoint != request.Endpoint;

        // Использование богатой доменной модели (DDD)
        var domainDevice = device.ToDomain();
        domainDevice.UpdateDetails(request.Name, newRoomId, request.Protocol, request.Endpoint);
        device.ApplyDomainChanges(domainDevice);

        await _db.SaveChangesAsync();

        if (isChanged)
        {
            await _bus.Publish(new DeviceUpdatedEvent(
                device.NetworkDeviceId,
                device.Name,
                device.RoomId.HasValue ? device.RoomId.Value.ToString() : null,
                device.IsRevoked,
                device.UpdatedAt ?? DateTime.UtcNow));
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

        // Использование метода доменного агрегата Revoke() (DDD)
        var domainDevice = device.ToDomain();
        domainDevice.Revoke();
        device.ApplyDomainChanges(domainDevice);

        await _db.SaveChangesAsync();

        await _bus.Publish(new DeviceRevokedEvent(
            device.NetworkDeviceId,
            userId,
            device.UpdatedAt ?? DateTime.UtcNow));

        _logger.LogInformation("Device {DeviceId} revoked by user {UserId}", id, userId);
        return NoContent();
    }

    [HttpPost]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.NetworkDeviceId))
            return BadRequest(new { error = "NetworkDeviceId is required" });

        var existing = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == request.NetworkDeviceId
                                   && d.OwnerUserId == userId
                                   && !d.IsRevoked);

        if (existing != null)
            return Conflict(new { error = $"Устройство с ID \"{request.NetworkDeviceId}\" уже зарегистрировано в вашем аккаунте." });

        var credential = new Infrastructure.Persistence.DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = request.NetworkDeviceId,
            SecretHash = "N/A",
            Name = request.Name ?? request.NetworkDeviceId,
            OwnerUserId = userId,
            Protocol = request.Protocol ?? "HTTP",
            Endpoint = request.Endpoint ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _db.DeviceCredentials.Add(credential);
        await _db.SaveChangesAsync();

        var dto = new DeviceDto(
            credential.NetworkDeviceId,
            credential.Name,
            credential.RoomId?.ToString(),
            credential.Protocol,
            credential.Endpoint,
            credential.IsRevoked,
            credential.CreatedAt);

        return CreatedAtAction(nameof(GetDevice), new { id = credential.NetworkDeviceId }, dto);
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


public record DeviceDto(
    string NetworkDeviceId,
    string? Name,
    string? RoomId,
    string? Protocol,      
    string? Endpoint,      
    bool IsRevoked,
    DateTime CreatedAt);

public record UpdateDeviceRequest(
    string? Name,
    string? RoomId,
    string? Protocol,     
    string? Endpoint);     

public record CreateDeviceRequest(
    string NetworkDeviceId,
    string? Name,
    string? Type,
    string? Protocol,
    string? Endpoint);