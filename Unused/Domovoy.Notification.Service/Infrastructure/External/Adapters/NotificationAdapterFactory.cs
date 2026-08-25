using System;
using System.Collections.Generic;
using System.Linq;

namespace Domovoy.Notification.Service.Infrastructure.External.Adapters;

public class NotificationAdapterFactory : INotificationAdapterFactory
{
    private readonly IEnumerable<INotificationAdapter> _adapters;

    public NotificationAdapterFactory(IEnumerable<INotificationAdapter> adapters)
    {
        _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
    }

    public INotificationAdapter? GetAdapter(string channelType)
    {
        return _adapters.FirstOrDefault(a => a.ChannelType.Equals(channelType, StringComparison.OrdinalIgnoreCase));
    }
}
