namespace Domovoy.Notification.Service.Infrastructure.External;

public interface INotificationSender
{
    string ChannelType { get; }
    Task SendAsync(string recipient, string subject, string message);
}