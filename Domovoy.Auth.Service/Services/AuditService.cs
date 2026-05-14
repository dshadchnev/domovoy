using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Default implementation of IAuditService
/// </summary>
public class AuditService : IAuditService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AuthDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(Guid? userId, string? deviceId, string action, string resource, string result, string? ipAddress = null, string? failureReason = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceId = deviceId,
                Action = action,
                Resource = resource,
                Result = result,
                IpAddress = ipAddress,
                FailureReason = failureReason,
                CreatedAt = DateTime.UtcNow
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();

            _logger.LogDebug("Audit logged: {Action} on {Resource} - Result: {Result}", action, resource, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event");
            // Don't throw - audit logging failures shouldn't break the application
        }
    }

    public async Task LogUserActionAsync(Guid? userId, string action, string result, string? ipAddress = null, string? failureReason = null)
    {
        await LogAsync(userId, null, action, "User", result, ipAddress, failureReason);
    }

    public async Task LogDeviceActionAsync(Guid? userId, string deviceId, string action, string result, string? ipAddress = null, string? failureReason = null)
    {
        await LogAsync(userId, deviceId, action, "Device", result, ipAddress, failureReason);
    }
}
