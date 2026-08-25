using Domovoy.Auth.Service.Infrastructure.Persistence.Entities;
using Domovoy.Auth.Service.Presentation.Contracts;

namespace Domovoy.Auth.Service.Application.Services;

/// <summary>
/// Сервис для генерации JWT токена и усправления
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Генерация JWT токена пользователя
    /// </summary>
    string GenerateUserToken(AuthUser user);

    /// <summary>
    /// Генерация JWT токена устройства
    /// </summary>
    string GenerateDeviceToken(DeviceCredential device);

    /// <summary>
    /// Генерация refresh токена
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Сохдание и хранение refresh токена в БД
    /// </summary>
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, Guid? replacesTokenId = null);

    /// <summary>
    /// Получение конфигурации refresh токена
    /// </summary>
    TokenConfig GetTokenConfig();
}

/// <summary>
/// Настойки конигурации токена
/// </summary>
public record TokenConfig(
    int AccessTokenExpiryMinutes,
    int RefreshTokenExpiryDays,
    string Issuer,
    string Audience);
