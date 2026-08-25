using System.Security.Claims;
using Domovoy.RulesEngine.Service.Presentation.Controllers;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.RulesEngine.Service.Tests;

public class RulesControllerTests
{
    private readonly DbContextOptions<RulesEngineDbContext> _dbOptions;
    private readonly Mock<ILogger<RulesController>> _loggerMock;
    private readonly Guid _testUserId = Guid.NewGuid();

    public RulesControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<RulesEngineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _loggerMock = new Mock<ILogger<RulesController>>();
    }

    private RulesController CreateController(RulesEngineDbContext dbContext)
    {
        var controller = new RulesController(dbContext, _loggerMock.Object, new Domovoy.Domain.Services.DomainRuleEngine());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return controller;
    }

    [Fact]
    public async Task GetRules_ReturnsOnlyUserRules()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        db.Rules.AddRange(
            new Rule { Id = Guid.NewGuid(), Name = "Rule 1", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", UserId = _testUserId.ToString() },
            new Rule { Id = Guid.NewGuid(), Name = "Rule 2", DeviceId = "dev-2", Condition = "humidity > 50", Command = "turn_off", UserId = Guid.NewGuid().ToString() }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetRules();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var rules = Assert.IsAssignableFrom<IEnumerable<RuleDto>>(okResult.Value);
        Assert.Single(rules);
        Assert.Equal("Rule 1", rules.First().Name);
    }

    [Fact]
    public async Task CreateRule_ValidCondition_ReturnsCreatedAtAction()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var controller = CreateController(db);
        var request = new CreateRuleRequest("Temp Check", "dev-1", null, null, "temperature > 25", "turn_on", null, 1);

        // Act
        var result = await controller.CreateRule(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<RuleDto>(createdResult.Value);
        Assert.Equal("Temp Check", dto.Name);
        Assert.Equal("dev-1", dto.DeviceId);
        Assert.True(dto.IsActive);
        Assert.Equal(1, db.Rules.Count());
    }

    [Fact]
    public async Task CreateRule_InvalidConditionSyntax_ReturnsBadRequest()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var controller = CreateController(db);
        var request = new CreateRuleRequest("Bad Rule", "dev-1", null, null, "temperature >> 25 (((", "turn_on", null, 1);

        // Act
        var result = await controller.CreateRule(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetRule_ExistingRule_ReturnsOk()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var ruleId = Guid.NewGuid();
        db.Rules.Add(new Rule { Id = ruleId, Name = "Rule 1", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", UserId = _testUserId.ToString() });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetRule(ruleId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RuleDto>(okResult.Value);
        Assert.Equal(ruleId, dto.Id);
    }

    [Fact]
    public async Task GetRule_NonExistingOrOtherUserRule_ReturnsNotFound()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var ruleId = Guid.NewGuid();
        db.Rules.Add(new Rule { Id = ruleId, Name = "Other User Rule", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", UserId = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.GetRule(ruleId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateRule_ValidUpdate_ReturnsNoContent()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var ruleId = Guid.NewGuid();
        db.Rules.Add(new Rule { Id = ruleId, Name = "Old Name", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", UserId = _testUserId.ToString() });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var updateRequest = new UpdateRuleRequest("New Name", null, null, null, "temperature > 30", "turn_on", null, 5);

        // Act
        var result = await controller.UpdateRule(ruleId, updateRequest);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var updated = await db.Rules.FindAsync(ruleId);
        Assert.Equal("New Name", updated!.Name);
        Assert.Equal("temperature > 30", updated.Condition);
        Assert.Equal(5, updated.Priority);
    }

    [Fact]
    public async Task DeleteRule_ExistingRule_RemovesFromDb()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var ruleId = Guid.NewGuid();
        db.Rules.Add(new Rule { Id = ruleId, Name = "Rule to delete", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", UserId = _testUserId.ToString() });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.DeleteRule(ruleId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, db.Rules.Count());
    }

    [Fact]
    public async Task ToggleRule_ExistingRule_SwitchesIsActiveState()
    {
        // Arrange
        using var db = new RulesEngineDbContext(_dbOptions);
        var ruleId = Guid.NewGuid();
        db.Rules.Add(new Rule { Id = ruleId, Name = "Toggle Rule", DeviceId = "dev-1", Condition = "temperature > 20", Command = "turn_on", IsActive = true, UserId = _testUserId.ToString() });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act
        var result = await controller.ToggleRule(ruleId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var toggled = await db.Rules.FindAsync(ruleId);
        Assert.False(toggled!.IsActive);
    }
}
