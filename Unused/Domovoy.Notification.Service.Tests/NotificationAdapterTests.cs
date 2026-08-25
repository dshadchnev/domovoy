using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domovoy.Domain.Events;
using Domovoy.Notification.Service.Infrastructure.External;
using Domovoy.Notification.Service.Infrastructure.External.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Notification.Service.Tests;

public class NotificationAdapterTests
{
    private readonly Mock<INotificationSender> _telegramSenderMock = new();
    private readonly Mock<INotificationSender> _emailSenderMock = new();
    private readonly Mock<ILogger<TelegramNotificationAdapter>> _tgLoggerMock = new();
    private readonly Mock<ILogger<EmailNotificationAdapter>> _emailLoggerMock = new();

    public NotificationAdapterTests()
    {
        _telegramSenderMock.Setup(s => s.ChannelType).Returns("Telegram");
        _emailSenderMock.Setup(s => s.ChannelType).Returns("Email");
    }

    [Fact]
    public void NotificationAdapterFactory_GetAdapter_ReturnsCorrectAdapter()
    {
        var tgAdapter = new TelegramNotificationAdapter(_tgLoggerMock.Object);
        var emailAdapter = new EmailNotificationAdapter(_emailLoggerMock.Object);

        var factory = new NotificationAdapterFactory(new INotificationAdapter[] { tgAdapter, emailAdapter });

        Assert.Same(tgAdapter, factory.GetAdapter("Telegram"));
        Assert.Same(emailAdapter, factory.GetAdapter("Email"));
        Assert.Null(factory.GetAdapter("SMS"));
    }

    [Fact]
    public async Task TelegramNotificationAdapter_SendNotificationAsync_TranslatesAndDelegates()
    {
        var senders = new List<INotificationSender> { _telegramSenderMock.Object };
        var adapter = new TelegramNotificationAdapter(_tgLoggerMock.Object, senders: senders);

        var request = new NotificationRequested(
            UserId: Guid.NewGuid(),
            EventType: "RuleTriggered",
            Title: "High Temp Alert",
            Message: "Sensor reading 35C",
            ChannelType: "Telegram",
            RecipientAddress: "chat_12345"
        );

        await adapter.SendNotificationAsync(request);

        _telegramSenderMock.Verify(s => s.SendAsync("chat_12345", "High Temp Alert", "Sensor reading 35C"), Times.Once);
    }

    [Fact]
    public async Task EmailNotificationAdapter_SendNotificationAsync_TranslatesAndDelegates()
    {
        var senders = new List<INotificationSender> { _emailSenderMock.Object };
        var adapter = new EmailNotificationAdapter(_emailLoggerMock.Object, senders: senders);

        var request = new NotificationRequested(
            UserId: Guid.NewGuid(),
            EventType: "CommandFailed",
            Title: "Command Failed",
            Message: "Device disconnected",
            ChannelType: "Email",
            RecipientAddress: "user@domovoy.local"
        );

        await adapter.SendNotificationAsync(request);

        _emailSenderMock.Verify(s => s.SendAsync("user@domovoy.local", "Command Failed", "Device disconnected"), Times.Once);
    }
}
