using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.RulesEngine.Service.Data;
using Microsoft.EntityFrameworkCore;
using NCalc; 

namespace Domovoy.RulesEngine.Service.Consumers;

public class TelemetryRuleEvaluator : IConsumer<TelemetryReceivedEvent>
{
    private readonly IDbContextFactory<RulesEngineDbContext> _dbFactory;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<TelemetryRuleEvaluator> _logger;

    public TelemetryRuleEvaluator(
        IDbContextFactory<RulesEngineDbContext> dbFactory,
        IPublishEndpoint bus,
        ILogger<TelemetryRuleEvaluator> logger)
    {
        _dbFactory = dbFactory;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TelemetryReceivedEvent> context)
    {
        var telemetry = context.Message;
        _logger.LogDebug("[Evaluate] Evaluating rules for {DeviceId}", telemetry.DeviceId);

        // Парсим данные телеметрии
        var telemetryData = ParseTelemetryData(telemetry.Data);

        using var db = _dbFactory.CreateDbContext();

        // Загружаем активные правила для этого устройства, сортируем по приоритету
        var rules = await db.Rules
            .Where(r => r.DeviceId == telemetry.DeviceId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        foreach (var rule in rules)
        {
            try
            {
                if (EvaluateRule(rule.Condition, telemetryData))
                {
                    _logger.LogInformation("[Rule] Rule '{RuleName}' triggered for {DeviceId}",
                        rule.Name, telemetry.DeviceId);

                    // 1. Публикуем команду на выполнение в диспетчер команд
                    await _bus.Publish(new ExecuteCommandEvent(
                        rule.DeviceId,
                        rule.Command,
                        rule.CommandParams,
                        rule.Id.ToString(),
                        DateTime.UtcNow));

                    // 2. Публикуем событие срабатывания правила для Notification Service
                    var userId = Guid.TryParse(rule.UserId, out var uid) ? uid : Guid.Empty;
                    var tempVal = telemetryData.TryGetValue("temperature", out var t) ? $"{t}°C" : telemetry.Data;

                    await _bus.Publish(new RuleTriggeredEvent(
                        userId,
                        rule.Name,
                        rule.DeviceId,
                        tempVal,
                        rule.Command,
                        DateTime.UtcNow));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error evaluating rule {RuleId} for {DeviceId}",
                    rule.Id, telemetry.DeviceId);
            }
        }
    }

    private Dictionary<string, object> ParseTelemetryData(string rawData)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(rawData);
        var result = new Dictionary<string, object>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number =>
                    prop.Value.TryGetDouble(out var d) ? d : prop.Value.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => prop.Value.GetString()
            };
        }
        return result;
    }

    private bool EvaluateRule(string condition, Dictionary<string, object> variables)
    {
        var expr = new Expression(condition)
        {
            Parameters = variables
        };

        var result = expr.Evaluate();
        return result is bool b && b;
    }
}