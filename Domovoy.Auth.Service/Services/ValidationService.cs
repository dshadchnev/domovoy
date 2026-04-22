using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Data;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Default implementation of IValidationService
/// </summary>
public class ValidationService : IValidationService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(AuthDbContext db, ILogger<ValidationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateUserRegistrationAsync(UserRegisterRequest request)
    {
        _logger.LogDebug("Validating user registration for username: {Username}", request.Username);

        // Check required fields
        if (string.IsNullOrWhiteSpace(request.Username))
            return (false, "Username is required");

        if (string.IsNullOrWhiteSpace(request.Email))
            return (false, "Email is required");

        if (string.IsNullOrWhiteSpace(request.Password))
            return (false, "Password is required");

        // Validate email format
        var emailValidator = new EmailAddressAttribute();
        if (!emailValidator.IsValid(request.Email))
            return (false, "Email format is invalid");

        // Check username uniqueness
        if (await _db.Users.AnyAsync(u => u.UserName == request.Username))
            return (false, "Username already exists");

        // Check email uniqueness
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return (false, "Email already exists");

        // Validate password strength
        if (request.Password.Length < 8)
            return (false, "Password must be at least 8 characters");

        return (true, null);
    }

public (bool IsValid, string? ErrorMessage) ValidateUserLogin(UserLoginRequest request)
    {
        _logger.LogDebug("Validating user login for username: {Username}", request.Username);

        if (string.IsNullOrWhiteSpace(request.Username))
            return (false, "Username is required");

        if (string.IsNullOrWhiteSpace(request.Password))
            return (false, "Password is required");

        // Username must be at least 3 characters
        if (request.Username.Length < 3)
            return (false, "Username must be at least 3 characters");

        return (true, null);
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateDeviceRegistrationAsync(DeviceRegisterRequest request, Guid ownerUserId)
    {
        _logger.LogDebug("Validating device registration for device: {DeviceId}", request.NetworkDeviceId);

        if (string.IsNullOrWhiteSpace(request.NetworkDeviceId))
            return (false, "Device ID is required");

        // Check device uniqueness
        var existing = await _db.DeviceCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == request.NetworkDeviceId);

        if (existing != null && !existing.IsRevoked)
            return (false, "Device already registered");

        return (true, null);
    }

    public (bool IsValid, string? ErrorMessage) ValidateDeviceAuth(DeviceAuthRequest request)
    {
        _logger.LogDebug("Validating device authentication");

        if (string.IsNullOrWhiteSpace(request.NetworkDeviceId))
            return (false, "Device ID is required");

        if (string.IsNullOrWhiteSpace(request.Secret))
            return (false, "Secret is required");

        return (true, null);
    }

    public (bool IsValid, string? ErrorMessage) ValidateRefreshToken(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return (false, "Refresh token is required");

        return (true, null);
    }
}
