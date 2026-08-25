using Domovoy.Domain.Entities;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;

namespace Domovoy.RulesEngine.Service.Application.Mappers;

/// <summary>
/// Маппер между доменной сущностью Scenario/ScenarioCondition/ScenarioAction и инфраструктурным сервисом правил Rule.
/// </summary>
public static class ScenarioDomainMapper
{
    public static Scenario ToDomainScenario(this Rule rule)
    {
        if (rule == null) return null!;

        var scenario = new Scenario
        {
            Id = rule.Id,
            Name = rule.Name,
            IsActive = rule.IsActive,
            UserId = Guid.TryParse(rule.UserId, out var uid) ? uid : Guid.Empty
        };

        // Разбор текстового условия NCalc в доменное условие ScenarioCondition
        var parts = rule.Condition.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            var targetSensorId = !string.IsNullOrEmpty(rule.SensorDeviceId) ? rule.SensorDeviceId : rule.DeviceId;
            scenario.AddCondition(new ScenarioCondition
            {
                ConditionType = "Telemetry",
                Operator = parts[1],
                Value = parts[2],
                DeviceId = Guid.TryParse(targetSensorId, out var did) ? did : null
            });
        }
        else
        {
            scenario.AddCondition(new ScenarioCondition
            {
                ConditionType = "TelemetryExpression",
                Operator = "==" ,
                Value = rule.Condition
            });
        }

        scenario.AddAction(new ScenarioAction
        {
            ActionType = rule.Command,
            Parameters = rule.CommandParams ?? string.Empty
        });

        return scenario;
    }

    public static Rule ToRule(
        this Scenario scenario,
        string deviceId,
        string rawCondition,
        string command,
        string? commandParams,
        int priority = 0)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));

        return new Rule
        {
            Id = scenario.Id,
            Name = scenario.Name,
            DeviceId = deviceId,
            Condition = rawCondition,
            Command = command,
            CommandParams = commandParams,
            Priority = priority,
            UserId = scenario.UserId.ToString(),
            IsActive = scenario.IsActive,
            CreatedAt = scenario.CreatedAt
        };
    }
}
