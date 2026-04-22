namespace Domovoy.Auth.Service.Contracts;

public record DeviceCredentialResponse(
    string NetworkDeviceId,
    string Secret,
    string Message = "Store the secret securely. It won't be shown again.");
