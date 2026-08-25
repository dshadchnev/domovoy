using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domovoy.Notification.Service.Presentation.Controllers;
using Domovoy.Notification.Service.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Notification.Service.Tests;

public class NotificationSettingsControllerTests
{
    private readonly DbContextOptions<NotificationDbContext> _dbOptions;
    private readonly Mock<ILogger<NotificationSettingsController>> _loggerMock = new();

    public NotificationSettingsControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private NotificationSettingsController CreateControllerWithUser(NotificationDbContext db, Guid userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new NotificationSettingsController(db, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    [Fact]
    public async Task GetSettings_ReturnsUserSpecificSettingsAndChannels()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using var db = new NotificationDbContext(_dbOptions);
        db.NotificationSettings.Add(new NotificationSetting { UserId = userId, EventType = "RuleTriggered", TelegramEnabled = true });
        db.NotificationSettings.Add(new NotificationSetting { UserId = otherUserId, EventType = "RuleTriggered", TelegramEnabled = false });

        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "123", IsActive = true });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Email", ChannelValue = "a@a.com", IsActive = false }); // inactive

        await db.SaveChangesAsync();

        var controller = CreateControllerWithUser(db, userId);

        // Act
        var result = await controller.GetSettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic data = okResult.Value!;
        var settings = (List<NotificationSetting>)data.GetType().GetProperty("settings").GetValue(data, null);
        var channels = (List<UserNotificationChannel>)data.GetType().GetProperty("channels").GetValue(data, null);

        Assert.Single(settings);
        Assert.Equal(userId, settings[0].UserId);
        Assert.Single(channels);
        Assert.Equal("Telegram", channels[0].ChannelType);
    }

    [Fact]
    public async Task UpdateSettings_CreatesNewSetting_IfDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var controller = CreateControllerWithUser(db, userId);
        var request = new UpdateSettingsRequest(TelegramEnabled: true, EmailEnabled: false);

        // Act
        var result = await controller.UpdateSettings("CommandFailed", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var settings = Assert.IsType<NotificationSetting>(okResult.Value);
        Assert.Equal(userId, settings.UserId);
        Assert.Equal("CommandFailed", settings.EventType);
        Assert.True(settings.TelegramEnabled);
        Assert.False(settings.EmailEnabled);

        var dbSetting = await db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == "CommandFailed");
        Assert.NotNull(dbSetting);
        Assert.True(dbSetting.TelegramEnabled);
    }

    [Fact]
    public async Task UpdateSettings_ModifiesExistingSetting_IfExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var existing = new NotificationSetting { UserId = userId, EventType = "CommandFailed", TelegramEnabled = false, EmailEnabled = true };
        db.NotificationSettings.Add(existing);
        await db.SaveChangesAsync();

        var controller = CreateControllerWithUser(db, userId);
        var request = new UpdateSettingsRequest(TelegramEnabled: true, EmailEnabled: false);

        // Act
        await controller.UpdateSettings("CommandFailed", request);

        // Assert
        var dbSetting = await db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.EventType == "CommandFailed");
        Assert.NotNull(dbSetting);
        Assert.True(dbSetting.TelegramEnabled);
        Assert.False(dbSetting.EmailEnabled);
        Assert.NotNull(dbSetting.UpdatedAt);
    }

    [Fact]
    public async Task AddChannel_SavesChannelToDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var controller = CreateControllerWithUser(db, userId);
        var request = new AddChannelRequest(ChannelType: "Telegram", ChannelValue: "chat_id_999");

        // Act
        var result = await controller.AddChannel(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var channel = Assert.IsType<UserNotificationChannel>(createdResult.Value);
        Assert.Equal(userId, channel.UserId);
        Assert.Equal("Telegram", channel.ChannelType);
        Assert.Equal("chat_id_999", channel.ChannelValue);
        Assert.True(channel.IsActive);

        var dbChannel = await db.UserNotificationChannels.FindAsync(channel.Id);
        Assert.NotNull(dbChannel);
    }

    [Fact]
    public async Task DeleteChannel_SetsIsActiveToFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var channel = new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "chat_id", IsActive = true };
        db.UserNotificationChannels.Add(channel);
        await db.SaveChangesAsync();

        var controller = CreateControllerWithUser(db, userId);

        // Act
        var result = await controller.DeleteChannel(channel.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var dbChannel = await db.UserNotificationChannels.FindAsync(channel.Id);
        Assert.NotNull(dbChannel);
        Assert.False(dbChannel.IsActive);
    }

    [Fact]
    public async Task DeleteChannel_ReturnsNotFound_IfChannelDoesNotBelongToUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var channel = new UserNotificationChannel { UserId = Guid.NewGuid(), ChannelType = "Telegram", ChannelValue = "chat_id", IsActive = true };
        db.UserNotificationChannels.Add(channel);
        await db.SaveChangesAsync();

        var controller = CreateControllerWithUser(db, userId);

        // Act
        var result = await controller.DeleteChannel(channel.Id);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetLogs_ReturnsLogsSortedByDate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var db = new NotificationDbContext(_dbOptions);
        var oldLog = new NotificationLog { UserId = userId, EventType = "CommandFailed", Channel = "Email", Message = "old", CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
        var newLog = new NotificationLog { UserId = userId, EventType = "CommandFailed", Channel = "Email", Message = "new", CreatedAt = DateTime.UtcNow };

        db.NotificationLogs.AddRange(oldLog, newLog);
        await db.SaveChangesAsync();

        var controller = CreateControllerWithUser(db, userId);

        // Act
        var result = await controller.GetLogs(10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var logs = Assert.IsType<List<NotificationLog>>(okResult.Value);
        Assert.Equal(2, logs.Count);
        Assert.Equal("new", logs[0].Message); // Sorted descending
    }
}
