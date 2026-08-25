using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domovoy.Notification.Service.Presentation.Consumers;
using Domovoy.Notification.Service.Infrastructure.Persistence;
using Domovoy.Notification.Service.Infrastructure.External;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Notification.Service.Tests;

public class CommandFailedConsumerTests
{
    private readonly DbContextOptions<NotificationDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<NotificationDbContext>> _dbFactoryMock = new();
    private readonly Mock<INotificationSender> _telegramSenderMock = new();
    private readonly Mock<INotificationSender> _emailSenderMock = new();
    private readonly Mock<ILogger<CommandFailedConsumer>> _loggerMock = new();

    public CommandFailedConsumerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _telegramSenderMock.Setup(s => s.ChannelType).Returns("Telegram");
        _emailSenderMock.Setup(s => s.ChannelType).Returns("Email");
    }

    [Fact]
    public async Task Consume_SendsTelegramAndEmail_WhenBothEnabledAndConfigured()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = "dev-01";

        using var db = new NotificationDbContext(_dbOptions);
        // Add replica device credential to find OwnerUserId
        db.DeviceCredentials.Add(new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = deviceId, OwnerUserId = userId });
        // Add settings
        db.NotificationSettings.Add(new NotificationSetting { UserId = userId, EventType = "CommandFailed", TelegramEnabled = true, EmailEnabled = true });
        // Add active notification channels
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "telegram_chat_123", IsActive = true });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Email", ChannelValue = "user@test.com", IsActive = true });
        await db.SaveChangesAsync();

        _dbFactoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new NotificationDbContext(_dbOptions));

        var senders = new List<INotificationSender> { _telegramSenderMock.Object, _emailSenderMock.Object };
        var consumer = new CommandFailedConsumer(_dbFactoryMock.Object, senders, _loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<CommandFailedEvent>>();
        contextMock.Setup(c => c.Message).Returns(new CommandFailedEvent(deviceId, "turn_on", "Timeout", DateTime.UtcNow));

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _telegramSenderMock.Verify(s => s.SendAsync("telegram_chat_123", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _emailSenderMock.Verify(s => s.SendAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        using var checkDb = new NotificationDbContext(_dbOptions);
        var logs = await checkDb.NotificationLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal("sent", l.Status));
        Assert.Contains(logs, l => l.Channel == "Telegram");
        Assert.Contains(logs, l => l.Channel == "Email");
    }

    [Fact]
    public async Task Consume_DoesNotSend_WhenChannelIsDisabledInSettings()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = "dev-02";

        using var db = new NotificationDbContext(_dbOptions);
        db.DeviceCredentials.Add(new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = deviceId, OwnerUserId = userId });
        // Telegram enabled, Email disabled in settings
        db.NotificationSettings.Add(new NotificationSetting { UserId = userId, EventType = "CommandFailed", TelegramEnabled = true, EmailEnabled = false });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "tg_123", IsActive = true });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Email", ChannelValue = "email@test.com", IsActive = true });
        await db.SaveChangesAsync();

        _dbFactoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new NotificationDbContext(_dbOptions));

        var senders = new List<INotificationSender> { _telegramSenderMock.Object, _emailSenderMock.Object };
        var consumer = new CommandFailedConsumer(_dbFactoryMock.Object, senders, _loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<CommandFailedEvent>>();
        contextMock.Setup(c => c.Message).Returns(new CommandFailedEvent(deviceId, "turn_off", "Error", DateTime.UtcNow));

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _telegramSenderMock.Verify(s => s.SendAsync("tg_123", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _emailSenderMock.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        using var checkDb = new NotificationDbContext(_dbOptions);
        var logs = await checkDb.NotificationLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("Telegram", logs[0].Channel);
    }

    [Fact]
    public async Task Consume_LogsFailure_WhenSenderThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = "dev-03";

        using var db = new NotificationDbContext(_dbOptions);
        db.DeviceCredentials.Add(new DeviceCredential { Id = Guid.NewGuid(), NetworkDeviceId = deviceId, OwnerUserId = userId });
        db.NotificationSettings.Add(new NotificationSetting { UserId = userId, EventType = "CommandFailed", TelegramEnabled = true, EmailEnabled = false });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "tg_123", IsActive = true });
        await db.SaveChangesAsync();

        _telegramSenderMock.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Telegram network error"));

        _dbFactoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new NotificationDbContext(_dbOptions));

        var senders = new List<INotificationSender> { _telegramSenderMock.Object };
        var consumer = new CommandFailedConsumer(_dbFactoryMock.Object, senders, _loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<CommandFailedEvent>>();
        contextMock.Setup(c => c.Message).Returns(new CommandFailedEvent(deviceId, "turn_on", "Timeout", DateTime.UtcNow));

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var checkDb = new NotificationDbContext(_dbOptions);
        var log = await checkDb.NotificationLogs.SingleAsync();
        Assert.Equal("failed", log.Status);
        Assert.Equal("Telegram network error", log.ErrorMessage);
    }
}
