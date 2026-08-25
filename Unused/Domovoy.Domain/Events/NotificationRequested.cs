using System;

namespace Domovoy.Domain.Events
{
    /// <summary>
    /// Доменное событие/намерение отправки уведомления пользователю.
    /// </summary>
    public record NotificationRequested(
        Guid UserId,
        string EventType,
        string Title,
        string Message,
        string ChannelType,
        string RecipientAddress
    );
}
