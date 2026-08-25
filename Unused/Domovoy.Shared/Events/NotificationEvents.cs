using System;

namespace Domovoy.Shared.Events;

public record CommandFailedEvent(
    string DeviceId,
    string Command,
    string ErrorMessage,
    DateTime Timestamp);

public record RuleTriggeredEvent(
    Guid UserId,
    string RuleName,
    string DeviceId,
    string Value,
    string Command,
    DateTime Timestamp);
