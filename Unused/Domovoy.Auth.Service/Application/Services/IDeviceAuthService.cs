using Domovoy.Auth.Service.Presentation.Contracts;
using Domovoy.Auth.Service.Infrastructure.Persistence.Entities;

namespace Domovoy.Auth.Service.Application.Services;

public interface IDeviceAuthService
    {
        Task<DeviceCredentialResponse> RegisterAsync(DeviceRegisterRequest req, Guid ownerUserId, string? ipAddress = null);
        Task<DeviceTokenResponse> AuthenticateAsync(DeviceAuthRequest req, string? ipAddress = null);
        Task RevokeDeviceAsync(string networkDeviceId, Guid userId);
        Task RotateSecretAsync(string networkDeviceId, Guid userId);
    }
