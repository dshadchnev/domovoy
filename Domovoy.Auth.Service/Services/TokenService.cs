using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Default implementation of ITokenService
/// </summary>
public class TokenService : ITokenService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<TokenService> _logger;
    private readonly TokenConfig _config;
    private readonly SymmetricSecurityKey _key;

    public TokenService(AuthDbContext db, ILogger<TokenService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;

        var jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
        var expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"] ?? "60");
        var refreshExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var issuer = config["Jwt:Issuer"] ?? "domovoy";
        var audience = config["Jwt:Audience"] ?? "domovoy-users";

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        _config = new TokenConfig(expiryMinutes, refreshExpiryDays, issuer, audience);
    }

    public string GenerateUserToken(AuthUser user)
    {
        _logger.LogDebug("Generating JWT token for user {UserId}", user.Id);

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.UserName!),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email!),
            new System.Security.Claims.Claim("FirstName", user.FirstName),
            new System.Security.Claims.Claim("LastName", user.LastName),
            new System.Security.Claims.Claim("Type", "User"),
        };

        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: _config.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_config.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateDeviceToken(DeviceCredential device)
    {
        _logger.LogDebug("Generating JWT token for device {DeviceId}", device.NetworkDeviceId);

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim("DeviceId", device.NetworkDeviceId),
            new System.Security.Claims.Claim("OwnerId", device.OwnerUserId.ToString()),
            new System.Security.Claims.Claim("RoomId", device.RoomId?.ToString() ?? ""),
            new System.Security.Claims.Claim("Type", "Device"),
        };

        var deviceTokenExpiryMinutes = int.Parse(_config.Audience.Contains("device") ? "1440" : "1440");
        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: "domovoy-devices",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(deviceTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        _logger.LogDebug("Generating refresh token");

        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, Guid? replacesTokenId = null)
    {
        var token = GenerateRefreshToken();
        var tokenHash = HashToken(token);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_config.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow,
            ReplacedByTokenId = null
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Refresh token created for user {UserId}", userId);
        return refreshToken;
    }

    public TokenConfig GetTokenConfig() => _config;

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }
}
