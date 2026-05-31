namespace Domovoy.DeviceManager.Service.Events;

/// <summary>
/// Событие публикуется при изменении метаданных устройства.
/// Используется для синхронизации кэша в Redis, обновления правил в Rules Engine и т.д.
/// </summary>
public record DeviceUpdatedEvent(
    string DeviceId,
    string? Name,
    string? RoomId,
    bool IsRevoked,
    DateTime UpdatedAt);