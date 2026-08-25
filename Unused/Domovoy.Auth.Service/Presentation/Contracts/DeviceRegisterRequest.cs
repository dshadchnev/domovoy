namespace Domovoy.Auth.Service.Presentation.Contracts;

public record DeviceRegisterRequest(
    string NetworkDeviceId,
    Guid? RoomId = null);
