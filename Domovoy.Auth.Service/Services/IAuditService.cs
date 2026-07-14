namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Сервис для ведения журнала аудита событий
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Лог события аудита
    /// </summary>
    Task LogAsync(Guid? userId, string? deviceId, string action, string resource, string result, string? ipAddress = null, string? failureReason = null);

    /// <summary>
    /// Лог действий пользователя
    /// </summary>
    Task LogUserActionAsync(Guid? userId, string action, string result, string? ipAddress = null, string? failureReason = null);

    /// <summary>
    /// Лог действий устройства 
    /// </summary>
    Task LogDeviceActionAsync(Guid? userId, string deviceId, string action, string result, string? ipAddress = null, string? failureReason = null);
}
