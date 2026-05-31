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
        _logger.LogDebug("🔍 Evaluating rules for {DeviceId}", telemetry.DeviceId);

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
                    _logger.LogInformation("✅ Rule '{RuleName}' triggered for {DeviceId}",
                        rule.Name, telemetry.DeviceId);

                    // Публикуем команду на выполнение
                    await _bus.Publish(new ExecuteCommandEvent(
                        rule.DeviceId,
                        rule.Command,
                        rule.CommandParams,
                        rule.Id.ToString(),
                        DateTime.UtcNow));

                    // Опционально: прервать после первого сработавшего правила
                    // break;
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
        // Простой парсер: {"temperature": 23.5, "status": "ON"} → Dictionary
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
        // Используем NCalc для безопасного вычисления выражений
        // Пример условия: "temperature > 25 && status == 'ON'"
        var expr = new Expression(condition)
        {
            Parameters = variables
        };

        // Добавляем поддержку функций, если нужно
        // expr.EvaluateFunction += (name, args) => { ... };

        var result = expr.Evaluate();
        return result is bool b && b;
    }
}