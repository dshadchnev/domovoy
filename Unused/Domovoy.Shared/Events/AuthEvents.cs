namespace Domovoy.Shared.Events;

public record UserRegisteredEvent(Guid UserId, string Email, string Role);
public record DeviceLinkedEvent(string NetworkDeviceId, Guid OwnerId, Guid? RoomId);
public record UserRolesChangedEvent(Guid UserId, string NewRole);
public record DeviceSecretRotatedEvent(string NetworkDeviceId);

// 📊 Audit Events
public record AuthAuditEvent(
    Guid Id,
    Guid? UserId,
    string? DeviceId,
    string Action,
    string Resource,
    string Result,
    string? IpAddress,
    string? FailureReason,
    DateTime CreatedAt);

public record UserLoggedInEvent(Guid UserId, string IpAddress, DateTime Timestamp);
public record UserLoggedOutEvent(Guid UserId, DateTime Timestamp);
public record TokenRefreshedEvent(Guid UserId, DateTime Timestamp);
public record DeviceAuthenticatedEvent(string DeviceId, Guid OwnerId, DateTime Timestamp);
public record DeviceRevokedEvent(string DeviceId, Guid OwnerId, DateTime Timestamp);