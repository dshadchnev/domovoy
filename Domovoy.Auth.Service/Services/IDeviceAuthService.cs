using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Data.Entities;

namespace Domovoy.Auth.Service.Services
{
    public interface IDeviceAuthService
    {
        Task<DeviceCredentialResponse> RegisterAsync(DeviceRegisterRequest req, Guid ownerUserId, string? ipAddress = null);
        Task<DeviceTokenResponse> AuthenticateAsync(DeviceAuthRequest req, string? ipAddress = null);
        Task RevokeDeviceAsync(string networkDeviceId, Guid userId);
        Task RotateSecretAsync(string networkDeviceId, Guid userId);
    }
}
