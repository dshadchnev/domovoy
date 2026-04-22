namespace Domovoy.Auth.Service.Contracts;

public record DeviceAuthRequest(
    string NetworkDeviceId,
    string Secret);
