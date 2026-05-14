using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Shared.Events;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Domovoy.Auth.Service.Services;

public interface IUserAuthService
{
    Task<UserResponse> RegisterAsync(UserRegisterRequest req, string? ipAddress = null);
    Task<TokenResponse> LoginAsync(UserLoginRequest req, string? ipAddress = null);
    Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest req, string? ipAddress = null);
    Task RevokeTokenAsync(string token);
    Task LogoutAsync(Guid userId);
}

public class UserAuthService : IUserAuthService
{
    private readonly AuthDbContext _db;
    private readonly IPasswordHasher<AuthUser> _hasher;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<UserAuthService> _logger;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IValidationService _validationService;

    public UserAuthService(
        AuthDbContext db,
        IPublishEndpoint bus,
        ILogger<UserAuthService> logger,
        ITokenService tokenService,
        IAuditService auditService,
        IValidationService validationService)
    {
        _db = db;
        _hasher = new PasswordHasher<AuthUser>();
        _bus = bus;
        _logger = logger;
        _tokenService = tokenService;
        _auditService = auditService;
        _validationService = validationService;
    }

    public async Task<UserResponse> RegisterAsync(UserRegisterRequest req, string? ipAddress = null)
    {
        _logger.LogInformation("Processing user registration for username: {Username}", req.Username);

        // Validate request
        var (isValid, errorMessage) = await _validationService.ValidateUserRegistrationAsync(req);
        if (!isValid)
        {
            _logger.LogWarning("User registration validation failed: {ErrorMessage}", errorMessage);
            await _auditService.LogUserActionAsync(null, "USER_REGISTER", "Failure", ipAddress, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // Create user
        var user = new AuthUser
        {
            Id = Guid.NewGuid(),
            UserName = req.Username,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered successfully: {UserId}", user.Id);
        
        // Publish event and audit log
        await _bus.Publish(new UserRegisteredEvent(user.Id, user.Email!, "User"));
        await _auditService.LogUserActionAsync(user.Id, "USER_REGISTER", "Success", ipAddress);

        return MapToUserResponse(user);
    }

    public async Task<TokenResponse> LoginAsync(UserLoginRequest req, string? ipAddress = null)
    {
        _logger.LogInformation("Processing user login for username: {Username}", req.Username);

        // Validate request
        var (isValid, errorMessage) = _validationService.ValidateUserLogin(req);
        if (!isValid)
        {
            _logger.LogWarning("User login validation failed: {ErrorMessage}", errorMessage);
            await _auditService.LogUserActionAsync(null, "USER_LOGIN", "Failure", ipAddress, errorMessage);
            throw new UnauthorizedAccessException(errorMessage);
        }

        // Find user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == req.Username);
        
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login failed: user not found or inactive - {Username}", req.Username);
            await _auditService.LogUserActionAsync(null, "USER_LOGIN", "Failure", ipAddress, "Invalid credentials");
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Verify password
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash!, req.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed: invalid password - {UserId}", user.Id);
            await _auditService.LogUserActionAsync(user.Id, "USER_LOGIN", "Failure", ipAddress, "Invalid password");
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateUserToken(user);
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id);

        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);
        await _auditService.LogUserActionAsync(user.Id, "USER_LOGIN", "Success", ipAddress);
        
        // Publish event
        await _bus.Publish(new UserLoggedInEvent(user.Id, ipAddress ?? "unknown", DateTime.UtcNow));

        var config = _tokenService.GetTokenConfig();
        return new TokenResponse(
            accessToken,
            refreshToken.Token,
            config.AccessTokenExpiryMinutes * 60);
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest req, string? ipAddress = null)
    {
        _logger.LogDebug("Processing token refresh");

        // Validate request
        var (isValid, errorMessage) = _validationService.ValidateRefreshToken(req);
        if (!isValid)
        {
            _logger.LogWarning("Token refresh validation failed: {ErrorMessage}", errorMessage);
            await _auditService.LogUserActionAsync(null, "TOKEN_REFRESH", "Failure", ipAddress, errorMessage);
            throw new UnauthorizedAccessException(errorMessage);
        }

        // Find refresh token
        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow);

        if (refreshToken is null)
        {
            _logger.LogWarning("Token refresh failed: invalid or expired token");
            await _auditService.LogUserActionAsync(null, "TOKEN_REFRESH", "Failure", ipAddress, "Invalid or expired token");
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _db.Users.FindAsync(refreshToken.UserId);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Token refresh failed: user inactive or not found");
            await _auditService.LogUserActionAsync(refreshToken.UserId, "TOKEN_REFRESH", "Failure", ipAddress, "User inactive");
            throw new UnauthorizedAccessException("User is inactive");
        }

        // Generate new tokens
        var newAccessToken = _tokenService.GenerateUserToken(user);
        var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, refreshToken.Id);

        // Revoke old token
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByTokenId = newRefreshToken.Id;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Token refreshed successfully for user: {UserId}", user.Id);
        await _auditService.LogUserActionAsync(user.Id, "TOKEN_REFRESH", "Success", ipAddress);
        
        // Publish event
        await _bus.Publish(new TokenRefreshedEvent(user.Id, DateTime.UtcNow));

        var config = _tokenService.GetTokenConfig();
        return new TokenResponse(
            newAccessToken,
            newRefreshToken.Token,
            config.AccessTokenExpiryMinutes * 60);
    }

    public async Task RevokeTokenAsync(string token)
    {
        _logger.LogDebug("Revoking token");

        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken is null)
            return;

        refreshToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Token revoked for user: {UserId}", refreshToken.UserId);
    }

    public async Task LogoutAsync(Guid userId)
    {
        _logger.LogInformation("Processing logout for user: {UserId}", userId);

        // Revoke all active tokens
        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        
        _logger.LogInformation("User logged out successfully: {UserId}", userId);
        
        // Publish event
        await _bus.Publish(new UserLoggedOutEvent(userId, DateTime.UtcNow));
        await _auditService.LogUserActionAsync(userId, "USER_LOGOUT", "Success");
    }

    private static UserResponse MapToUserResponse(AuthUser user)
    {
        return new UserResponse(
            user.Id,
            user.UserName!,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.CreatedAt);
    }
}
