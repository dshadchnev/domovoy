using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Auth.Service.Contracts;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Service for JWT token generation and management
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generate JWT token for user
    /// </summary>
    string GenerateUserToken(AuthUser user);

    /// <summary>
    /// Generate JWT token for device
    /// </summary>
    string GenerateDeviceToken(DeviceCredential device);

    /// <summary>
    /// Generate random refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Create and store refresh token in database
    /// </summary>
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, Guid? replacesTokenId = null);

    /// <summary>
    /// Get refresh token configuration
    /// </summary>
    TokenConfig GetTokenConfig();
}

/// <summary>
/// Token configuration settings
/// </summary>
public record TokenConfig(
    int AccessTokenExpiryMinutes,
    int RefreshTokenExpiryDays,
    string Issuer,
    string Audience);
