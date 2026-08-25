namespace Domovoy.Auth.Service.Presentation.Contracts;

public record DeviceAuthRequest(
    string NetworkDeviceId,
    string Secret);
