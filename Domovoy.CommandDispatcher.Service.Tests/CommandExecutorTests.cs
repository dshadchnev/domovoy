using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domovoy.CommandDispatcher.Service.Consumers;
using Domovoy.CommandDispatcher.Service.Data;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Domovoy.CommandDispatcher.Service.Tests;

public class CommandExecutorTests
{
    private readonly Mock<IDbContextFactory<DispatcherDbContext>> _dbFactoryMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<CommandExecutor>> _loggerMock;
    private readonly DbContextOptions<DispatcherDbContext> _dbOptions;

    public CommandExecutorTests()
    {
        _dbFactoryMock = new Mock<IDbContextFactory<DispatcherDbContext>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<CommandExecutor>>();

        _dbOptions = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new DispatcherDbContext(_dbOptions));
    }

    [Fact]
    public async Task Consume_DeviceNotFound_LogsFailedCommand()
    {
        // Arrange
        var contextMock = new Mock<ConsumeContext<ExecuteCommandEvent>>();
        var commandEvent = new ExecuteCommandEvent("device-1", "turn_on", "{}", "rule-1", DateTime.UtcNow);
        contextMock.Setup(c => c.Message).Returns(commandEvent);

        var consumer = new CommandExecutor(_dbFactoryMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var db = new DispatcherDbContext(_dbOptions);
        var loggedCommand = await db.CommandLogs.FirstOrDefaultAsync();
        Assert.NotNull(loggedCommand);
        Assert.Equal("device-1", loggedCommand.DeviceId);
        Assert.Equal("failed", loggedCommand.Status);
        Assert.Equal("Device not found", loggedCommand.ErrorMessage);
    }

    [Fact]
    public async Task Consume_HttpProtocol_SendsPostAndLogsSuccess()
    {
        // Arrange
        using (var dbSetup = new DispatcherDbContext(_dbOptions))
        {
            dbSetup.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                NetworkDeviceId = "device-1",
                Protocol = "HTTP",
                Endpoint = "http://localhost:9999/api/command",
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });
            await dbSetup.SaveChangesAsync();
        }

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("DeviceHttp")).Returns(httpClient);

        var contextMock = new Mock<ConsumeContext<ExecuteCommandEvent>>();
        var commandEvent = new ExecuteCommandEvent("device-1", "turn_on", "{\"brightness\": 80}", "rule-1", DateTime.UtcNow);
        contextMock.Setup(c => c.Message).Returns(commandEvent);

        var consumer = new CommandExecutor(_dbFactoryMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var db = new DispatcherDbContext(_dbOptions);
        var loggedCommand = await db.CommandLogs.FirstOrDefaultAsync();
        Assert.NotNull(loggedCommand);
        Assert.Equal("device-1", loggedCommand.DeviceId);
        Assert.Equal("HTTP", loggedCommand.Protocol);
        Assert.Equal("http://localhost:9999/api/command", loggedCommand.Endpoint);
        Assert.Equal("success", loggedCommand.Status);
        Assert.Null(loggedCommand.ErrorMessage);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri == new Uri("http://localhost:9999/api/command")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Consume_HttpProtocol_HttpFails_LogsFailedCommandAndThrows()
    {
        // Arrange
        using (var dbSetup = new DispatcherDbContext(_dbOptions))
        {
            dbSetup.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                NetworkDeviceId = "device-1",
                Protocol = "HTTP",
                Endpoint = "http://localhost:9999/api/command",
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });
            await dbSetup.SaveChangesAsync();
        }

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("DeviceHttp")).Returns(httpClient);

        var contextMock = new Mock<ConsumeContext<ExecuteCommandEvent>>();
        var commandEvent = new ExecuteCommandEvent("device-1", "turn_on", "{}", "rule-1", DateTime.UtcNow);
        contextMock.Setup(c => c.Message).Returns(commandEvent);

        var consumer = new CommandExecutor(_dbFactoryMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => consumer.Consume(contextMock.Object));

        using var db = new DispatcherDbContext(_dbOptions);
        var loggedCommand = await db.CommandLogs.FirstOrDefaultAsync();
        Assert.NotNull(loggedCommand);
        Assert.Equal("failed", loggedCommand.Status);
        Assert.Contains("500", loggedCommand.ErrorMessage);
    }

    [Fact]
    public async Task Consume_MqttProtocol_LogsSuccess()
    {
        // Arrange
        using (var dbSetup = new DispatcherDbContext(_dbOptions))
        {
            dbSetup.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                NetworkDeviceId = "device-1",
                Protocol = "MQTT",
                Endpoint = "devices/device-1/cmd",
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });
            await dbSetup.SaveChangesAsync();
        }

        var contextMock = new Mock<ConsumeContext<ExecuteCommandEvent>>();
        var commandEvent = new ExecuteCommandEvent("device-1", "turn_off", "{}", "rule-1", DateTime.UtcNow);
        contextMock.Setup(c => c.Message).Returns(commandEvent);

        var consumer = new CommandExecutor(_dbFactoryMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var db = new DispatcherDbContext(_dbOptions);
        var loggedCommand = await db.CommandLogs.FirstOrDefaultAsync();
        Assert.NotNull(loggedCommand);
        Assert.Equal("device-1", loggedCommand.DeviceId);
        Assert.Equal("MQTT", loggedCommand.Protocol);
        Assert.Equal("devices/device-1/cmd", loggedCommand.Endpoint);
        Assert.Equal("success", loggedCommand.Status);
    }

    [Fact]
    public async Task Consume_ZigbeeProtocol_LogsSuccess()
    {
        // Arrange
        using (var dbSetup = new DispatcherDbContext(_dbOptions))
        {
            dbSetup.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                NetworkDeviceId = "device-1",
                Protocol = "ZIGBEE",
                Endpoint = "0x00158d0001d82e1c/1",
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            });
            await dbSetup.SaveChangesAsync();
        }

        var contextMock = new Mock<ConsumeContext<ExecuteCommandEvent>>();
        var commandEvent = new ExecuteCommandEvent("device-1", "toggle", null, "rule-1", DateTime.UtcNow);
        contextMock.Setup(c => c.Message).Returns(commandEvent);

        var consumer = new CommandExecutor(_dbFactoryMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var db = new DispatcherDbContext(_dbOptions);
        var loggedCommand = await db.CommandLogs.FirstOrDefaultAsync();
        Assert.NotNull(loggedCommand);
        Assert.Equal("device-1", loggedCommand.DeviceId);
        Assert.Equal("ZIGBEE", loggedCommand.Protocol);
        Assert.Equal("0x00158d0001d82e1c/1", loggedCommand.Endpoint);
        Assert.Equal("success", loggedCommand.Status);
    }
}
