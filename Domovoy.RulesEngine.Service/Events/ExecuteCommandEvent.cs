namespace Domovoy.Shared.Events;

/// <summary>
/// Событие для выполнения команды на устройстве
/// </summary>
public record ExecuteCommandEvent(

    // Целевое устройство
    string DeviceId,

    // "turn_on", "set_brightness", "set_temperature"
    string Command,

    // JSON с параметрами: {"brightness": 80}
    string? Params,

    // Какое правило сработало (для аудита)
    string? SourceRuleId,

    // Время генерации команды
    DateTime Timestamp);       