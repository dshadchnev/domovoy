using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;
using Rule = Domovoy.RulesEngine.Service.Infrastructure.Persistence.Rule;
using Xunit;

namespace Domovoy.RulesEngine.Service.Tests;

public class PostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("test_rules_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task PostgresIntegration_CreateAndQueryRule_Success()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<RulesEngineDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using var db = new RulesEngineDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Postgres Test Rule",
            SensorDeviceId = "sensor-pg-1",
            Condition = "temperature > 30",
            Command = "turn_on_fan",
            IsActive = true,
            Priority = 1,
            UserId = "user-pg-1"
        };

        // Act
        db.Rules.Add(rule);
        await db.SaveChangesAsync();

        var savedRule = await db.Rules.FirstOrDefaultAsync(r => r.SensorDeviceId == "sensor-pg-1");

        // Assert
        Assert.NotNull(savedRule);
        Assert.Equal("Postgres Test Rule", savedRule.Name);
        Assert.Equal("turn_on_fan", savedRule.Command);
    }
}
