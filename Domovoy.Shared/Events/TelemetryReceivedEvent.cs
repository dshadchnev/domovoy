namespace Domovoy.Shared.Events;

public record TelemetryReceivedEvent(string DeviceId, string Data, DateTime Timestamp);