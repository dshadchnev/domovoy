using System.Threading;
using System.Threading.Tasks;
using Domovoy.Domain.Events;

namespace Domovoy.Notification.Service.Infrastructure.External.Adapters;

/// <summary>
/// Переведено: Адаптер Anti-Corruption Layer (ACL) для трансляции доменного запроса уведомления в протокол-специфичные API.
/// Переведено: </summary>
public interface INotificationAdapter
{
    string ChannelType { get; }
    Task SendNotificationAsync(NotificationRequested notification, CancellationToken cancellationToken = default);
}
