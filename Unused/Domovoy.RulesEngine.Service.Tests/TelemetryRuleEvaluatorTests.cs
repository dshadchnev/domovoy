using Domovoy.RulesEngine.Service.Presentation.Consumers;
using Domovoy.RulesEngine.Service.Application.Pipeline;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.RulesEngine.Service.Tests;

public class TelemetryRuleEvaluatorTests
{
    private readonly DbContextOptions<RulesEngineDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<RulesEngineDbContext>> _dbFactoryMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<TelemetryRuleEvaluator>> _loggerMock;

    public TelemetryRuleEvaluatorTests()
    {
        _dbOptions = new DbContextOptionsBuilder<RulesEngineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbFactoryMock = new Mock<IDbContextFactory<RulesEngineDbContext>>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<TelemetryRuleEvaluator>>();
    }

    [Fact]
    public async Task Consume_ConditionMet_PublishesExecuteCommandEvent()
    {
        // Arrange
        using (var db = new RulesEngineDbContext(_dbOptions))
        {
            db.Rules.Add(new Rule
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-123",
                Name = "High Temp Alert",
                Condition = "temperature > 25.0",
                Command = "turn_on_cooler",
                CommandParams = "{\"speed\": 5}",
                IsActive = true,
                Priority = 1,
                UserId = Guid.NewGuid().ToString()
            });
            await db.SaveChangesAsync();
        }

        _dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new RulesEngineDbContext(_dbOptions));

        var pipeline = new TelemetryPipeline(new ITelemetryStep[]
        {
            new TelemetryValidationStep(new Mock<ILogger<TelemetryValidationStep>>().Object),
            new TelemetryNormalizationStep()
        });

        var evaluator = new TelemetryRuleEvaluator(_dbFactoryMock.Object, _publishEndpointMock.Object, _loggerMock.Object, new Domovoy.Domain.Services.DomainRuleEngine(), pipeline);

        var consumeContextMock = new Mock<ConsumeContext<TelemetryReceivedEvent>>();
        consumeContextMock.Setup(c => c.Message).Returns(new TelemetryReceivedEvent(
            "dev-123",
            "{\"temperature\": 26.5, \"humidity\": 60}",
            DateTime.UtcNow
        ));

        // Act
        await evaluator.Consume(consumeContextMock.Object);

        // Assert
        _publishEndpointMock.Verify(p => p.Publish(
            It.Is<ExecuteCommandEvent>(e =>
                e.DeviceId == "dev-123" &&
                e.Command == "turn_on_cooler" &&
                e.Params == "{\"speed\": 5}"
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Consume_ConditionNotMet_DoesNotPublishEvent()
    {
        // Arrange
        using (var db = new RulesEngineDbContext(_dbOptions))
        {
            db.Rules.Add(new Rule
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-123",
                Name = "High Temp Alert",
                Condition = "temperature > 30.0",
                Command = "turn_on_cooler",
                IsActive = true,
                Priority = 1,
                UserId = Guid.NewGuid().ToString()
            });
            await db.SaveChangesAsync();
        }

        _dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new RulesEngineDbContext(_dbOptions));

        var pipeline = new TelemetryPipeline(new ITelemetryStep[]
        {
            new TelemetryValidationStep(new Mock<ILogger<TelemetryValidationStep>>().Object),
            new TelemetryNormalizationStep()
        });

        var evaluator = new TelemetryRuleEvaluator(_dbFactoryMock.Object, _publishEndpointMock.Object, _loggerMock.Object, new Domovoy.Domain.Services.DomainRuleEngine(), pipeline);

        var consumeContextMock = new Mock<ConsumeContext<TelemetryReceivedEvent>>();
        consumeContextMock.Setup(c => c.Message).Returns(new TelemetryReceivedEvent(
            "dev-123",
            "{\"temperature\": 26.5}",
            DateTime.UtcNow
        ));

        // Act
        await evaluator.Consume(consumeContextMock.Object);

        // Assert
        _publishEndpointMock.Verify(p => p.Publish(
            It.IsAny<ExecuteCommandEvent>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task Consume_InactiveRule_DoesNotPublishEvent()
    {
        // Arrange
        using (var db = new RulesEngineDbContext(_dbOptions))
        {
            db.Rules.Add(new Rule
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-123",
                Name = "Disabled Rule",
                Condition = "temperature > 20.0",
                Command = "turn_on_cooler",
                IsActive = false,
                Priority = 1,
                UserId = Guid.NewGuid().ToString()
            });
            await db.SaveChangesAsync();
        }

        _dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new RulesEngineDbContext(_dbOptions));

        var pipeline = new TelemetryPipeline(new ITelemetryStep[]
        {
            new TelemetryValidationStep(new Mock<ILogger<TelemetryValidationStep>>().Object),
            new TelemetryNormalizationStep()
        });

        var evaluator = new TelemetryRuleEvaluator(_dbFactoryMock.Object, _publishEndpointMock.Object, _loggerMock.Object, new Domovoy.Domain.Services.DomainRuleEngine(), pipeline);

        var consumeContextMock = new Mock<ConsumeContext<TelemetryReceivedEvent>>();
        consumeContextMock.Setup(c => c.Message).Returns(new TelemetryReceivedEvent(
            "dev-123",
            "{\"temperature\": 26.5}",
            DateTime.UtcNow
        ));

        // Act
        await evaluator.Consume(consumeContextMock.Object);

        // Assert
        _publishEndpointMock.Verify(p => p.Publish(
            It.IsAny<ExecuteCommandEvent>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }
}
