using System.Text.Json;
using Domovoy.Shared.Events;

namespace Domovoy.RulesEngine.Service.Application.Pipeline;

public class TelemetryValidationStep : ITelemetryStep
{
    private readonly ILogger<TelemetryValidationStep> _logger;

    public TelemetryValidationStep(ILogger<TelemetryValidationStep> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(TelemetryContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Telemetry.DeviceId))
        {
            context.IsValid = false;
            context.ValidationErrorMessage = "DeviceId is empty";
            _logger.LogWarning("❌ TelemetryValidationStep failed: empty DeviceId");
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Telemetry.Data))
        {
            context.IsValid = false;
            context.ValidationErrorMessage = "Data is empty";
            _logger.LogWarning("❌ TelemetryValidationStep failed: empty Data for device {DeviceId}", context.Telemetry.DeviceId);
            return;
        }

        await next();
    }
}

public class TelemetryNormalizationStep : ITelemetryStep
{
    public Task ProcessAsync(TelemetryContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        var dataDict = new Dictionary<string, object?>();
        try
        {
            using var doc = JsonDocument.Parse(context.Telemetry.Data);
            foreach (var element in doc.RootElement.EnumerateObject())
            {
                dataDict[element.Name.ToLowerInvariant()] = element.Value.ValueKind switch
                {
                    JsonValueKind.Number => element.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => element.Value.GetString(),
                    _ => element.Value.GetRawText()
                };
            }
        }
        catch
        {
            dataDict["raw"] = context.Telemetry.Data;
        }

        context.ParsedData = dataDict;
        return next();
    }
}

public class TelemetryPipeline
{
    private readonly IEnumerable<ITelemetryStep> _steps;

    public TelemetryPipeline(IEnumerable<ITelemetryStep> steps)
    {
        _steps = steps;
    }

    public async Task ExecuteAsync(TelemetryContext context, CancellationToken cancellationToken = default)
    {
        var enumerator = _steps.GetEnumerator();

        Task Next()
        {
            if (enumerator.MoveNext())
            {
                return enumerator.Current.ProcessAsync(context, Next, cancellationToken);
            }
            return Task.CompletedTask;
        }

        await Next();
    }
}
