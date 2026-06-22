using System.Security.Claims;
using Domovoy.CommandDispatcher.Service.Controllers;
using Domovoy.CommandDispatcher.Service.Data;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Domovoy.CommandDispatcher.Service.Tests;

public class CommandsControllerTests
{
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<CommandsController>> _loggerMock;
    private readonly DbContextOptions<DispatcherDbContext> _dbOptions;

    public CommandsControllerTests()
    {
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<CommandsController>>();

        _dbOptions = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private CommandsController CreateController(DispatcherDbContext db, Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new CommandsController(db, _publishEndpointMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    [Fact]
    public async Task GetCommands_ReturnsCommandsForUserDevices()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            db.DeviceCredentials.AddRange(
                new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "user-device", OwnerUserId = userId, IsRevoked = false, CreatedAt = DateTime.UtcNow },
                new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "other-device", OwnerUserId = otherUserId, IsRevoked = false, CreatedAt = DateTime.UtcNow }
            );

            db.CommandLogs.AddRange(
                new CommandLog { Id = Guid.NewGuid(), DeviceId = "user-device", Command = "turn_on", Status = "success", CreatedAt = DateTime.UtcNow },
                new CommandLog { Id = Guid.NewGuid(), DeviceId = "other-device", Command = "turn_off", Status = "success", CreatedAt = DateTime.UtcNow }
            );

            await db.SaveChangesAsync();
        }

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            var controller = CreateController(db, userId);

            // Act
            var result = await controller.GetCommands(null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var commands = Assert.IsAssignableFrom<IEnumerable<CommandLogDto>>(okResult.Value);
            var commandList = commands.ToList();

            Assert.Single(commandList);
            Assert.Equal("user-device", commandList[0].DeviceId);
        }
    }

    [Fact]
    public async Task GetCommand_ReturnsCommand_WhenOwnedByUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            db.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(), NetworkDeviceId = "user-device", OwnerUserId = userId, IsRevoked = false, CreatedAt = DateTime.UtcNow
            });
            db.CommandLogs.Add(new CommandLog
            {
                Id = commandId, DeviceId = "user-device", Command = "turn_on", Status = "success", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            var controller = CreateController(db, userId);

            // Act
            var result = await controller.GetCommand(commandId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var command = Assert.IsType<CommandLogDto>(okResult.Value);
            Assert.Equal(commandId, command.Id);
        }
    }

    [Fact]
    public async Task GetCommand_ReturnsNotFound_WhenNotOwnedByUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            db.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(), NetworkDeviceId = "other-device", OwnerUserId = otherUserId, IsRevoked = false, CreatedAt = DateTime.UtcNow
            });
            db.CommandLogs.Add(new CommandLog
            {
                Id = commandId, DeviceId = "other-device", Command = "turn_on", Status = "success", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            var controller = CreateController(db, userId);

            // Act
            var result = await controller.GetCommand(commandId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }

    [Fact]
    public async Task RetryCommand_QueuesAndPublishes_WhenFailedAndOwnedByUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            db.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(), NetworkDeviceId = "user-device", OwnerUserId = userId, IsRevoked = false, CreatedAt = DateTime.UtcNow
            });
            db.CommandLogs.Add(new CommandLog
            {
                Id = commandId, DeviceId = "user-device", Command = "turn_on", Status = "failed", ErrorMessage = "HTTP Timeout", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            var controller = CreateController(db, userId);

            // Act
            var result = await controller.RetryCommand(commandId);

            // Assert
            Assert.IsType<AcceptedResult>(result);

            var command = await db.CommandLogs.FirstOrDefaultAsync(c => c.Id == commandId);
            Assert.NotNull(command);
            Assert.Equal("pending", command.Status);
            Assert.Null(command.ErrorMessage);

            _publishEndpointMock.Verify(p => p.Publish(
                It.Is<ExecuteCommandEvent>(e => e.DeviceId == "user-device" && e.Command == "turn_on"),
                It.IsAny<CancellationToken>()), Times.Once());
        }
    }

    [Fact]
    public async Task RetryCommand_ReturnsBadRequest_WhenNotFailed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            db.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(), NetworkDeviceId = "user-device", OwnerUserId = userId, IsRevoked = false, CreatedAt = DateTime.UtcNow
            });
            db.CommandLogs.Add(new CommandLog
            {
                Id = commandId, DeviceId = "user-device", Command = "turn_on", Status = "success", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = new DispatcherDbContext(_dbOptions))
        {
            var controller = CreateController(db, userId);

            // Act
            var result = await controller.RetryCommand(commandId);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
