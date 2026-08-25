using System.Collections.Generic;
using Domovoy.Domain.Entities;
using Domovoy.Domain.Services;
using Xunit;

namespace Domovoy.RulesEngine.Service.Tests;

public class DomainRuleEngineTests
{
    private readonly DomainRuleEngine _engine = new();

    [Fact]
    public void EvaluateCondition_VariousOperators_ReturnsExpectedResult()
    {
        var conditionGreater = new ScenarioCondition { Operator = ">", Value = "25.0" };
        Assert.True(_engine.EvaluateCondition(conditionGreater, 26.0m));
        Assert.False(_engine.EvaluateCondition(conditionGreater, 24.0m));

        var conditionEquals = new ScenarioCondition { Operator = "==", Value = "100" };
        Assert.True(_engine.EvaluateCondition(conditionEquals, 100m));
        Assert.False(_engine.EvaluateCondition(conditionEquals, 99m));

        var conditionLessOrEqual = new ScenarioCondition { Operator = "<=", Value = "50" };
        Assert.True(_engine.EvaluateCondition(conditionLessOrEqual, 50m));
        Assert.True(_engine.EvaluateCondition(conditionLessOrEqual, 40m));
        Assert.False(_engine.EvaluateCondition(conditionLessOrEqual, 50.1m));
    }

    [Fact]
    public void EvaluateExpression_ValidFormula_EvaluatesCorrectly()
    {
        var vars = new Dictionary<string, object?>
        {
            { "temperature", 28.5 },
            { "humidity", 45 }
        };

        Assert.True(_engine.EvaluateExpression("temperature > 25.0 && humidity < 50", vars));
        Assert.False(_engine.EvaluateExpression("temperature > 30.0", vars));
    }

    [Fact]
    public void ValidateExpression_ValidAndInvalidSyntax_ReturnsCorrectBool()
    {
        Assert.True(_engine.ValidateExpression("temperature > 25.0"));
        Assert.False(_engine.ValidateExpression("temperature >> 25.0 ((("));
        Assert.False(_engine.ValidateExpression(""));
    }

    [Fact]
    public void EvaluateScenario_InactiveScenario_ReturnsFalse()
    {
        var scenario = new Scenario
        {
            IsActive = false
        };
        scenario.AddCondition(new ScenarioCondition { ConditionType = "Telemetry", Operator = ">", Value = "20" });

        var vars = new Dictionary<string, object?> { { "temp", 30 } };

        Assert.False(_engine.EvaluateScenario(scenario, "temp > 20", vars));
    }

    [Fact]
    public void EvaluateScenario_ActiveScenarioWithCondition_ReturnsTrue()
    {
        var scenario = new Scenario { IsActive = true };
        scenario.AddCondition(new ScenarioCondition { ConditionType = "Telemetry", Operator = ">", Value = "25.0" });

        var vars = new Dictionary<string, object?> { { "temperature", 30.0 } };

        Assert.True(_engine.EvaluateScenario(scenario, null, vars));
    }

    [Fact]
    public void EvaluateScenario_FallbackToRawCondition_ReturnsTrue()
    {
        var scenario = new Scenario { IsActive = true };

        var vars = new Dictionary<string, object?> { { "temperature", 30.0 } };

        Assert.True(_engine.EvaluateScenario(scenario, "temperature > 25.0", vars));
    }
}
