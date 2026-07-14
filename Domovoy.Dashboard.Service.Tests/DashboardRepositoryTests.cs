using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Domovoy.Dashboard.Service.Data;
using Domovoy.Dashboard.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Domovoy.Dashboard.Service.Tests;

public class DashboardRepositoryTests
{
    private readonly DbContextOptions<DashboardDbContext> _dbOptions;
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();
    private readonly Mock<ILogger<DashboardRepository>> _loggerMock = new();

    public DashboardRepositoryTests()
    {
        _dbOptions = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_redisDbMock.Object);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesCorrectSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new DashboardDbContext(_dbOptions);

        var dev1 = new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-01", OwnerUserId = userId, Protocol = "HTTP", IsRevoked = false };
        var dev2 = new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-02", OwnerUserId = userId, Protocol = "MQTT", IsRevoked = true };
        var dev3 = new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-03", OwnerUserId = Guid.NewGuid(), Protocol = "HTTP", IsRevoked = false }; // other user

        var rule1 = new Rule { Id = Guid.NewGuid(), Name = "Rule 1", UserId = userId, IsActive = true };
        var rule2 = new Rule { Id = Guid.NewGuid(), Name = "Rule 2", UserId = userId, IsActive = false };

        var today = DateTime.UtcNow.Date;
        var cmd1 = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Status = "success", CreatedAt = today.AddHours(2) };
        var cmd2 = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Status = "failed", CreatedAt = today.AddHours(4) };
        var cmdOld = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Status = "success", CreatedAt = today.AddDays(-1) }; // yesterday

        db.DeviceCredentials.AddRange(dev1, dev2, dev3);
        db.Rules.AddRange(rule1, rule2);
        db.CommandLogs.AddRange(cmd1, cmd2, cmdOld);
        await db.SaveChangesAsync();

        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);

        // Act
        var summary = await repo.GetSummaryAsync(userId);

        // Assert
        Assert.Equal(2, summary.TotalDevices);
        Assert.Equal(1, summary.ActiveDevices);
        Assert.Equal(2, summary.TotalRules);
        Assert.Equal(1, summary.ActiveRules);
        Assert.Equal(2, summary.CommandsToday);
        Assert.Equal(1, summary.CommandsSuccess);
        Assert.Equal(1, summary.CommandsFailed);
        Assert.Equal(1, summary.DevicesByProtocol["HTTP"]);
        Assert.Equal(1, summary.DevicesByProtocol["MQTT"]);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsDevicesWithStatusFromRedis()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new DashboardDbContext(_dbOptions);

        var dev = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = "dev-01",
            Name = "Living Room Light",
            OwnerUserId = userId,
            Protocol = "HTTP",
            Endpoint = "http://light",
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        db.DeviceCredentials.Add(dev);
        await db.SaveChangesAsync();

        var telemetryData = new Dictionary<string, object>
        {
            { "timestamp", DateTime.UtcNow.ToString("O") },
            { "status", "online" }
        };
        var redisValue = JsonSerializer.Serialize(telemetryData);

        _redisDbMock.Setup(r => r.StringGetAsync($"device:telemetry:{dev.NetworkDeviceId}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValue);

        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);

        // Act
        var devices = await repo.GetDevicesAsync(userId);

        // Assert
        Assert.Single(devices);
        var resultDevice = devices[0];
        Assert.Equal(dev.NetworkDeviceId, resultDevice.NetworkDeviceId);
        Assert.Equal(dev.Name, resultDevice.Name);
        Assert.Equal("online", resultDevice.LastStatus);
        Assert.NotNull(resultDevice.LastSeen);
    }

    [Fact]
    public async Task GetTelemetryHistoryAsync_ReturnsEmptyList()
    {
        // Arrange
        using var db = new DashboardDbContext(_dbOptions);
        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);
        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow;

        // Act
        var result = await repo.GetTelemetryHistoryAsync("dev-01", from, to);

        // Assert
        Assert.Equal("dev-01", result.DeviceId);
        Assert.Empty(result.Points);
    }

    [Fact]
    public async Task GetCommandStatsAsync_ComputesCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new DashboardDbContext(_dbOptions);

        var dev = new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-01", OwnerUserId = userId };
        db.DeviceCredentials.Add(dev);

        var today = DateTime.UtcNow;
        var cmd1 = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Command = "turn_on", Status = "success", CreatedAt = today.AddHours(-1) };
        var cmd2 = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Command = "turn_off", Status = "failed", CreatedAt = today.AddHours(-2) };
        var cmd3 = new CommandLog { Id = Guid.NewGuid(), DeviceId = "dev-01", Command = "turn_on", Status = "pending", CreatedAt = today.AddHours(-3) };

        db.CommandLogs.AddRange(cmd1, cmd2, cmd3);
        await db.SaveChangesAsync();

        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);

        // Act
        var stats = await repo.GetCommandStatsAsync(userId, today.AddHours(-4), today);

        // Assert
        Assert.Equal(3, stats.Total);
        Assert.Equal(1, stats.Success);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(1, stats.Pending);
        Assert.Equal(33.33, Math.Round(stats.SuccessRate, 2));
        Assert.Equal(3, stats.ByDevice["dev-01"]);
        Assert.Equal(2, stats.ByCommand["turn_on"]);
        Assert.Equal(1, stats.ByCommand["turn_off"]);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ReturnsCorrectLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new DashboardDbContext(_dbOptions);

        var dev = new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = "dev-01", OwnerUserId = userId };
        db.DeviceCredentials.Add(dev);

        for (int i = 0; i < 10; i++)
        {
            db.CommandLogs.Add(new CommandLog
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-01",
                Command = $"cmd-{i}",
                Status = "success",
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);

        // Act
        var commands = await repo.GetRecentCommandsAsync(userId, limit: 5);

        // Assert
        Assert.Equal(5, commands.Count);
        Assert.Equal("cmd-9", commands[0].Command); // Sorted descending by CreatedAt
    }

    [Fact]
    public async Task GetActiveRulesAsync_ReturnsOnlyActiveRules()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new DashboardDbContext(_dbOptions);

        var ruleActive = new Rule { Id = Guid.NewGuid(), Name = "Active", UserId = userId, IsActive = true, Priority = 2 };
        var ruleActiveHigh = new Rule { Id = Guid.NewGuid(), Name = "Active High", UserId = userId, IsActive = true, Priority = 1 };
        var ruleInactive = new Rule { Id = Guid.NewGuid(), Name = "Inactive", UserId = userId, IsActive = false, Priority = 3 };

        db.Rules.AddRange(ruleActive, ruleActiveHigh, ruleInactive);
        await db.SaveChangesAsync();

        var repo = new DashboardRepository(db, _redisMock.Object, _loggerMock.Object);

        // Act
        var activeRules = await repo.GetActiveRulesAsync(userId);

        // Assert
        Assert.Equal(2, activeRules.Count);
        Assert.Equal("Active High", activeRules[0].Name); // Sorted by priority ascending
    }
}
