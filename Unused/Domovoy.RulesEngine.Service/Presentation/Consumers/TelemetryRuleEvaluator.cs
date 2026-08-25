using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;
using Domovoy.RulesEngine.Service.Application.Mappers;
using Domovoy.Domain.Services;
using Microsoft.EntityFrameworkCore;

using Domovoy.RulesEngine.Service.Application.Pipeline;

namespace Domovoy.RulesEngine.Service.Presentation.Consumers;

public class TelemetryRuleEvaluator : IConsumer<TelemetryReceivedEvent>
{
    private readonly IDbContextFactory<RulesEngineDbContext> _dbFactory;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<TelemetryRuleEvaluator> _logger;
    private readonly IDomainRuleEngine _domainRuleEngine;
    private readonly TelemetryPipeline _pipeline;

    public TelemetryRuleEvaluator(
        IDbContextFactory<RulesEngineDbContext> dbFactory,
        IPublishEndpoint bus,
        ILogger<TelemetryRuleEvaluator> logger,
        IDomainRuleEngine domainRuleEngine,
        TelemetryPipeline pipeline)
    {
        _dbFactory = dbFactory;
        _bus = bus;
        _logger = logger;
        _domainRuleEngine = domainRuleEngine;
        _pipeline = pipeline;
    }

    public async Task Consume(ConsumeContext<TelemetryReceivedEvent> context)
    {
        var telemetry = context.Message;
        _logger.LogDebug("🔍 Evaluating rules for {DeviceId}", telemetry.DeviceId);

        // Переведено: Запуск Chain of Responsibility (Middleware Pipeline)
        var telemetryContext = new TelemetryContext(telemetry);
        await _pipeline.ExecuteAsync(telemetryContext, context.CancellationToken);

        if (!telemetryContext.IsValid)
        {
            _logger.LogWarning("⚠️ Telemetry rejected by pipeline for device {DeviceId}: {Reason}",
                telemetry.DeviceId, telemetryContext.ValidationErrorMessage);
            return;
        }

        var telemetryData = telemetryContext.ParsedData;

        using var db = _dbFactory.CreateDbContext();

        var rules = await db.Rules
            .Where(r => ((r.SensorDeviceId != null && r.SensorDeviceId == telemetry.DeviceId) || r.DeviceId == telemetry.DeviceId) && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        // Переведено: Параллельная оценка правил без блокировки потоков
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = context.CancellationToken
        };

        using var semaphore = new SemaphoreSlim(5);

        await Parallel.ForEachAsync(rules, parallelOptions, async (rule, ct) =>
        {
            try
            {
                // Переведено: Маппинг в доменный агрегат Scenario (DDD)
                var domainScenario = rule.ToDomainScenario();

                // Переведено: Вызов доменного сервиса IDomainRuleEngine для бизнес-оценки
                if (_domainRuleEngine.EvaluateScenario(domainScenario, rule.Condition, telemetryData))
                {
                    var targetActuator = !string.IsNullOrEmpty(rule.ActuatorDeviceId) ? rule.ActuatorDeviceId : rule.DeviceId;

                    _logger.LogInformation("✅ Scenario/Rule '{RuleName}' triggered! Sensor={SensorId} -> Actuator={ActuatorId}, Command={Command}",
                        rule.Name, telemetry.DeviceId, targetActuator, rule.Command);

                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await _bus.Publish(new ExecuteCommandEvent(
                            targetActuator,
                            rule.Command,
                            rule.CommandParams,
                            rule.Id.ToString(),
                            DateTime.UtcNow), ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error evaluating rule {RuleId} for {DeviceId}",
                    rule.Id, telemetry.DeviceId);
            }
        });
    }

    private Dictionary<string, object?> ParseTelemetryData(string rawData)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(rawData);
        var result = new Dictionary<string, object?>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number =>
                    prop.Value.TryGetDouble(out var d) ? d : prop.Value.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => prop.Value.GetString() ?? string.Empty
            };
        }
        return result;
    }
}