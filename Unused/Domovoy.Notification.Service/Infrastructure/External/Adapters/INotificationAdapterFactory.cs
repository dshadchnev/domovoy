namespace Domovoy.Notification.Service.Infrastructure.External.Adapters;

/// <summary>
/// Переведено: Фабрика/Диспетчер для выбора ACL-адаптера уведомлений по каналу связи.
/// Переведено: </summary>
public interface INotificationAdapterFactory
{
    INotificationAdapter? GetAdapter(string channelType);
}
