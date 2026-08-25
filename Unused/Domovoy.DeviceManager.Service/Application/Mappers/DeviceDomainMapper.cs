using Domovoy.DeviceManager.Service.Infrastructure.Persistence;
using Domovoy.Domain.Entities;

namespace Domovoy.DeviceManager.Service.Application.Mappers;

/// <summary>
/// Маппер между доменным агрегатом Device и инфраструктурной сущностью EF DeviceCredential
/// </summary>
public static class DeviceDomainMapper
{
    public static Device ToDomain(this DeviceCredential entity)
    {
        if (entity == null) return null!;

        return new Device
        {
            Id = entity.Id,
            DeviceId = entity.NetworkDeviceId,
            Name = entity.Name ?? entity.NetworkDeviceId,
            UserId = entity.OwnerUserId ?? Guid.Empty,
            RoomId = entity.RoomId,
            Protocol = entity.Protocol,
            Endpoint = entity.Endpoint,
            IsRevoked = entity.IsRevoked,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static void ApplyDomainChanges(this DeviceCredential entity, Device domain)
    {
        if (entity == null || domain == null) return;

        entity.Name = domain.Name;
        entity.RoomId = domain.RoomId;
        entity.Protocol = domain.Protocol;
        entity.Endpoint = domain.Endpoint;
        entity.IsRevoked = domain.IsRevoked;
        entity.UpdatedAt = domain.UpdatedAt;
    }
}
