namespace Domovoy.Notification.Service.Services;

public interface INotificationSender
{
    string ChannelType { get; }
    Task SendAsync(string recipient, string subject, string message);
}