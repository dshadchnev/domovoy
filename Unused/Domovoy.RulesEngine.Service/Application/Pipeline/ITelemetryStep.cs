using Domovoy.Shared.Events;

namespace Domovoy.RulesEngine.Service.Application.Pipeline;

public class TelemetryContext
{
    public TelemetryReceivedEvent Telemetry { get; set; }
    public Dictionary<string, object?> ParsedData { get; set; } = new();
    public bool IsValid { get; set; } = true;
    public string? ValidationErrorMessage { get; set; }

    public TelemetryContext(TelemetryReceivedEvent telemetry)
    {
        Telemetry = telemetry;
    }
}

public interface ITelemetryStep
{
    Task ProcessAsync(TelemetryContext context, Func<Task> next, CancellationToken cancellationToken);
}
