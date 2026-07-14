using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domovoy.Dashboard.Service.Controllers;
using Domovoy.Dashboard.Service.Data;
using Domovoy.Dashboard.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Dashboard.Service.Tests;

public class DashboardControllerTests
{
    private readonly Mock<IDashboardRepository> _repositoryMock = new();
    private readonly Mock<ILogger<DashboardController>> _loggerMock = new();

    private DashboardController CreateControllerWithUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new DashboardController(_repositoryMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private DashboardController CreateControllerWithoutUser()
    {
        return new DashboardController(_repositoryMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            }
        };
    }

    [Fact]
    public async Task GetSummary_ReturnsOk_WithExpectedSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedSummary = new DashboardSummary(
            TotalDevices: 5,
            ActiveDevices: 3,
            TotalRules: 2,
            ActiveRules: 1,
            CommandsToday: 10,
            CommandsSuccess: 8,
            CommandsFailed: 2,
            DevicesByProtocol: new Dictionary<string, int> { { "HTTP", 5 } },
            CommandsByStatus: new Dictionary<string, int> { { "success", 8 }, { "failed", 2 } }
        );

        _repositoryMock.Setup(r => r.GetSummaryAsync(userId))
            .ReturnsAsync(expectedSummary);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedSummary, okResult.Value);
    }

    [Fact]
    public async Task GetSummary_ThrowsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        var controller = CreateControllerWithoutUser();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetSummary());
    }

    [Fact]
    public async Task GetDevices_ReturnsOk_WithDevicesList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedDevices = new List<DeviceStatusDto>
        {
            new(Guid.NewGuid(), "dev-01", "Lamp", "HTTP", "http://local", false, DateTime.UtcNow, DateTime.UtcNow, "online")
        };

        _repositoryMock.Setup(r => r.GetDevicesAsync(userId))
            .ReturnsAsync(expectedDevices);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetDevices();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedDevices, okResult.Value);
    }

    [Fact]
    public async Task GetTelemetry_ReturnsOk_WithTelemetryHistory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = "dev-01";
        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow;
        var expectedTelemetry = new TelemetryHistoryDto(deviceId, from, to, new List<TelemetryPoint>());

        _repositoryMock.Setup(r => r.GetTelemetryHistoryAsync(deviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(expectedTelemetry);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetTelemetry(deviceId, from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedTelemetry, okResult.Value);
    }

    [Fact]
    public async Task GetCommandStats_ReturnsOk_WithStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var expectedStats = new CommandStatsDto(
            Total: 10, Success: 8, Failed: 2, Pending: 0, SuccessRate: 80.0,
            ByDevice: new Dictionary<string, int>(), ByCommand: new Dictionary<string, int>()
        );

        _repositoryMock.Setup(r => r.GetCommandStatsAsync(userId, from, to))
            .ReturnsAsync(expectedStats);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetCommandStats(from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedStats, okResult.Value);
    }

    [Fact]
    public async Task GetRecentCommands_ReturnsOk_WithLogs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedLogs = new List<CommandLog>
        {
            new() { Id = Guid.NewGuid(), DeviceId = "dev-01", Command = "turn_on", Status = "success", CreatedAt = DateTime.UtcNow }
        };

        _repositoryMock.Setup(r => r.GetRecentCommandsAsync(userId, 10))
            .ReturnsAsync(expectedLogs);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetRecentCommands(10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedLogs, okResult.Value);
    }

    [Fact]
    public async Task GetActiveRules_ReturnsOk_WithRules()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedRules = new List<Rule>
        {
            new() { Id = Guid.NewGuid(), Name = "Trigger Light", DeviceId = "light-01", Command = "turn_on", IsActive = true, UserId = userId }
        };

        _repositoryMock.Setup(r => r.GetActiveRulesAsync(userId))
            .ReturnsAsync(expectedRules);

        var controller = CreateControllerWithUser(userId);

        // Act
        var result = await controller.GetActiveRules();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedRules, okResult.Value);
    }
}
