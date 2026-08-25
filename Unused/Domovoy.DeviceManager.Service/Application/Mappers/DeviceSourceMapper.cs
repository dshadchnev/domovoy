using System;
using Riok.Mapperly.Abstractions;
using Domovoy.DeviceManager.Service.Infrastructure.Persistence;
using Domovoy.Domain.Entities;

namespace Domovoy.DeviceManager.Service.Application.Mappers;

/// <summary>
/// Source Generator mapper for Device domain aggregate and EF persistence entity
/// </summary>
[Mapper]
public partial class DeviceSourceMapper
{
    [MapProperty(nameof(DeviceCredential.NetworkDeviceId), nameof(Device.DeviceId))]
    [MapProperty(nameof(DeviceCredential.OwnerUserId), nameof(Device.UserId))]
    public partial Device ToDomain(DeviceCredential entity);

    public partial void UpdateEntity(Device domain, DeviceCredential entity);
}
