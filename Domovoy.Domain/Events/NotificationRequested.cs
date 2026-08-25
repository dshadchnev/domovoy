namespace Domovoy.Domain.Events;

/// <summary>
/// Domain event representing a request to send a notification to a user.
/// </summary>
public record NotificationRequested(
    string RecipientAddress,
    string Title,
    string Message,
    string Channel = "Email");
