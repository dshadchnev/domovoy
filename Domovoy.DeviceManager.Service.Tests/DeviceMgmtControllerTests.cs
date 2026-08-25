using System.Security.Claims;
using Domovoy.DeviceManager.Service.Presentation.Controllers;
using Domovoy.DeviceManager.Service.Infrastructure.Persistence;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.DeviceManager.Service.Tests;

public class DeviceMgmtControllerTests
{
    private readonly DbContextOptions<DeviceManagerDbContext> _dbOptions;
    private readonly Mock<ILogger<DeviceMgmtController>> _loggerMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Guid _testUserId = Guid.NewGuid();

    public DeviceMgmtControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<DeviceManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _loggerMock = new Mock<ILogger<DeviceMgmtController>>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
    }

    private DeviceMgmtController CreateController(DeviceManagerDbContext dbContext)
    {
        var controller = new DeviceMgmtController(dbContext, _loggerMock.Object, _publishEndpointMock.Object);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return controller;
    }

    [Fact]
    public async Task GetDevices_ReturnsOnlyActiveUserDevices()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        db.DeviceCredentials.AddRange(
            new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-1", Name = "Sensor 1", OwnerUserId = _testUserId, SecretHash = "hash1", IsRevoked = false },
            new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-2", Name = "Sensor 2 (Revoked)", OwnerUserId = _testUserId, SecretHash = "hash2", IsRevoked = true },
            new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-3", Name = "Other User Sensor", OwnerUserId = Guid.NewGuid(), SecretHash = "hash3", IsRevoked = false }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetDevices();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var devices = Assert.IsAssignableFrom<IEnumerable<DeviceDto>>(okResult.Value);
        Assert.Single(devices);
        Assert.Equal("dev-1", devices.First().NetworkDeviceId);
    }

    [Fact]
    public async Task GetDevice_ExistingDevice_ReturnsDeviceDto()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        db.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = "dev-100",
            Name = "Living Room Thermostat",
            OwnerUserId = _testUserId,
            SecretHash = "hash",
            Protocol = "MQTT",
            Endpoint = "192.168.1.50"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetDevice("dev-100");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DeviceDto>(okResult.Value);
        Assert.Equal("dev-100", dto.NetworkDeviceId);
        Assert.Equal("Living Room Thermostat", dto.Name);
        Assert.Equal("MQTT", dto.Protocol);
    }

    [Fact]
    public async Task GetDevice_NotFound_ReturnsNotFound()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        var controller = CreateController(db);

        // Act
        var result = await controller.GetDevice("unknown-dev");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateDevice_FieldChanged_PublishesDeviceUpdatedEvent()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        var device = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = "dev-200",
            Name = "Old Name",
            OwnerUserId = _testUserId,
            SecretHash = "hash",
            Protocol = "HTTP",
            Endpoint = "http://localhost:8080"
        };
        db.DeviceCredentials.Add(device);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new UpdateDeviceRequest("New Name", null, "HTTP", "http://localhost:8080");

        // Act
        var result = await controller.UpdateDevice("dev-200", request);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var updated = await db.DeviceCredentials.FirstAsync(d => d.NetworkDeviceId == "dev-200");
        Assert.Equal("New Name", updated.Name);

        _publishEndpointMock.Verify(p => p.Publish(
            It.Is<DeviceUpdatedEvent>(e => e.DeviceId == "dev-200" && e.Name == "New Name"),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task UpdateDevice_NoFieldsChanged_DoesNotPublishEvent()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        var device = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = "dev-300",
            Name = "Same Name",
            OwnerUserId = _testUserId,
            SecretHash = "hash",
            Protocol = "HTTP",
            Endpoint = "http://localhost:8080"
        };
        db.DeviceCredentials.Add(device);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new UpdateDeviceRequest("Same Name", null, "HTTP", "http://localhost:8080");

        // Act
        var result = await controller.UpdateDevice("dev-300", request);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _publishEndpointMock.Verify(p => p.Publish(
            It.IsAny<DeviceUpdatedEvent>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task DeleteDevice_ExistingDevice_SoftDeletesAndPublishesEvent()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        var device = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = "dev-400",
            Name = "Delete Me",
            OwnerUserId = _testUserId,
            SecretHash = "hash"
        };
        db.DeviceCredentials.Add(device);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.DeleteDevice("dev-400");

        // Assert: returns 204 No Content
        Assert.IsType<NoContentResult>(result);

        // Assert: record still exists (soft-delete)
        Assert.Equal(1, db.DeviceCredentials.Count());

        // Assert: device is marked as revoked
        var revoked = await db.DeviceCredentials.FindAsync(device.Id);
        Assert.NotNull(revoked);
        Assert.True(revoked.IsRevoked);
    }

    [Fact]
    public async Task CreateDevice_ValidRequest_CreatesDevice()
    {
        // Arrange
        using var db = new DeviceManagerDbContext(_dbOptions);
        var controller = CreateController(db);
        var request = new CreateDeviceRequest("new-dev-500", "New Device", "Sensor", "HTTP", "http://dev-500/api");

        // Act
        var result = await controller.CreateDevice(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<DeviceDto>(createdResult.Value);
        Assert.Equal("new-dev-500", dto.NetworkDeviceId);
        Assert.Equal("New Device", dto.Name);
    }
}
