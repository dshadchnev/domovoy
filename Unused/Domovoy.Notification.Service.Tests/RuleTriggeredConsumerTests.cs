using System;
using System.Collections.Generic;
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

public class RuleTriggeredConsumerTests
{
    private readonly DbContextOptions<NotificationDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<NotificationDbContext>> _dbFactoryMock = new();
    private readonly Mock<INotificationSender> _telegramSenderMock = new();
    private readonly Mock<INotificationSender> _emailSenderMock = new();
    private readonly Mock<ILogger<RuleTriggeredConsumer>> _loggerMock = new();

    public RuleTriggeredConsumerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _telegramSenderMock.Setup(s => s.ChannelType).Returns("Telegram");
        _emailSenderMock.Setup(s => s.ChannelType).Returns("Email");
    }

    [Fact]
    public async Task Consume_SendsTelegram_WhenTelegramEnabledAndConfigured()
    {
        // Arrange
        var userId = Guid.NewGuid();

        using var db = new NotificationDbContext(_dbOptions);
        db.NotificationSettings.Add(new NotificationSetting { UserId = userId, EventType = "RuleTriggered", TelegramEnabled = true, EmailEnabled = false });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Telegram", ChannelValue = "tg_chat_1", IsActive = true });
        db.UserNotificationChannels.Add(new UserNotificationChannel { UserId = userId, ChannelType = "Email", ChannelValue = "a@a.com", IsActive = true });
        await db.SaveChangesAsync();

        _dbFactoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new NotificationDbContext(_dbOptions));

        var senders = new List<INotificationSender> { _telegramSenderMock.Object, _emailSenderMock.Object };
        var consumer = new RuleTriggeredConsumer(_dbFactoryMock.Object, senders, _loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<RuleTriggeredEvent>>();
        contextMock.Setup(c => c.Message).Returns(new RuleTriggeredEvent(
            UserId: userId,
            RuleName: "High Temp Alarm",
            DeviceId: "temp-sensor-1",
            Value: "35C",
            Command: "turn_ac_on",
            Timestamp: DateTime.UtcNow
        ));

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _telegramSenderMock.Verify(s => s.SendAsync("tg_chat_1", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _emailSenderMock.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        using var checkDb = new NotificationDbContext(_dbOptions);
        var logs = await checkDb.NotificationLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("sent", logs[0].Status);
        Assert.Equal("Telegram", logs[0].Channel);
        Assert.Equal("RuleTriggered", logs[0].EventType);
    }
}
