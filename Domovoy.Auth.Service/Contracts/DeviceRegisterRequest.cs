namespace Domovoy.Auth.Service.Contracts;

public record DeviceRegisterRequest(
    string NetworkDeviceId,
    Guid? RoomId = null);
