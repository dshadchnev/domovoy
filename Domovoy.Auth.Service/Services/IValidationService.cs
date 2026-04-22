using Domovoy.Auth.Service.Contracts;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Service for validating user and device credentials
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validate user registration request
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateUserRegistrationAsync(UserRegisterRequest request);

    /// <summary>
    /// Validate user login request
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidateUserLogin(UserLoginRequest request);

    /// <summary>
    /// Validate device registration request
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateDeviceRegistrationAsync(DeviceRegisterRequest request, Guid ownerUserId);

    /// <summary>
    /// Validate device authentication request
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidateDeviceAuth(DeviceAuthRequest request);

    /// <summary>
    /// Validate refresh token request
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidateRefreshToken(RefreshTokenRequest request);
}
