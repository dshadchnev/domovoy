using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Domovoy.CommandDispatcher.Service.Infrastructure.Cache;

public class CachedDeviceMetadata
{
    public string DeviceId { get; set; } = string.Empty;
    public string Protocol { get; set; } = "HTTP";
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Кэш метаданных устройств с использованием слабосвязанных ссылок (WeakReference)
/// для исключения перегрузки LOH и GC при работе с тысячами устройств.
/// </summary>
public class DeviceCacheService
{
    private readonly ConcurrentDictionary<string, WeakReference<CachedDeviceMetadata>> _cache = new();

    public bool TryGet(string deviceId, out CachedDeviceMetadata? metadata)
    {
        metadata = null;
        if (_cache.TryGetValue(deviceId, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var target))
            {
                metadata = target;
                return true;
            }
            else
            {
                _cache.TryRemove(deviceId, out _);
            }
        }
        return false;
    }

    public void Set(string deviceId, CachedDeviceMetadata metadata)
    {
        _cache[deviceId] = new WeakReference<CachedDeviceMetadata>(metadata);
    }

    public void Invalidate(string deviceId)
    {
        _cache.TryRemove(deviceId, out _);
    }
}
