using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domovoy.Notification.Service.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Xunit;

namespace Domovoy.Notification.Service.Tests;

public class SenderTests
{
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<ILogger<TelegramSender>> _telegramLoggerMock = new();
    private readonly Mock<ILogger<EmailSender>> _emailLoggerMock = new();

    [Fact]
    public void TelegramSender_HasCorrectChannelType()
    {
        var sender = new TelegramSender(_botClientMock.Object, _telegramLoggerMock.Object);
        Assert.Equal("Telegram", sender.ChannelType);
    }

    [Fact]
    public async Task TelegramSender_SendAsync_ExecutesRequestOnBotClient()
    {
        // Arrange
        _botClientMock
            .Setup(x => x.SendRequest(
                It.IsAny<SendMessageRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message())
            .Verifiable();

        var sender = new TelegramSender(_botClientMock.Object, _telegramLoggerMock.Object);

        // Act
        await sender.SendAsync("12345", "Alert", "Test Message");

        // Assert
        _botClientMock.Verify();
    }

    [Fact]
    public void EmailSender_HasCorrectChannelType()
    {
        var inMemorySettings = new Dictionary<string, string?> {
            {"Smtp:Host", "localhost"},
            {"Smtp:Port", "587"},
            {"Smtp:User", "user"},
            {"Smtp:Pass", "pass"},
            {"Smtp:FromEmail", "test@test.com"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sender = new EmailSender(configuration, _emailLoggerMock.Object);
        Assert.Equal("Email", sender.ChannelType);
    }

    [Fact]
    public async Task EmailSender_SendAsync_ThrowsException_WhenHostNotConfigured()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Smtp:Host", null},
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sender = new EmailSender(configuration, _emailLoggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("a@a.com", "Subject", "Message"));
    }
}
