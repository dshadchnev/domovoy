namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Service for audit logging of security-related events
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(Guid? userId, string? deviceId, string action, string resource, string result, string? ipAddress = null, string? failureReason = null);

    /// <summary>
    /// Log user action
    /// </summary>
    Task LogUserActionAsync(Guid? userId, string action, string result, string? ipAddress = null, string? failureReason = null);

    /// <summary>
    /// Log device action
    /// </summary>
    Task LogDeviceActionAsync(Guid? userId, string deviceId, string action, string result, string? ipAddress = null, string? failureReason = null);
}
