namespace Domovoy.Shared.Events;

/// <summary>
/// Событие для выполнения команды на устройстве
/// </summary>
public record ExecuteCommandEvent(
    // Идентификатор устройства
    string DeviceId,

    // "turn_on", "set_brightness", "set_temperature"
    string Command,

    // JSON с параметрами: {"brightness": 80}
    string? Params,

    // Ссылка на правило автоматизации (для аудита)
    string? SourceRuleId,

    // Время создания события
    DateTime Timestamp);
