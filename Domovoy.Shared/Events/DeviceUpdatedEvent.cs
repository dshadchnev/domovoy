namespace Domovoy.Shared.Events;

/// <summary>
/// Публикуется при изменении метаданных устройства.
/// Используется для синхронизации кэша в Redis, обновления правил, уведомлений.
/// </summary>
public record DeviceUpdatedEvent(
    string DeviceId,
    string? Name,
    string? RoomId,
    bool IsRevoked,
    DateTime UpdatedAt);