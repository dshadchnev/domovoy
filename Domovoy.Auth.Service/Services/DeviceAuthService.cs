using Microsoft.AspNetCore.Identity;
using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Auth.Service.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Domovoy.Auth.Service.Services;

public class DeviceAuthService : IDeviceAuthService
{
    private readonly AuthDbContext _db;
    private readonly IPasswordHasher<string> _hasher = new PasswordHasher<string>();
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<DeviceAuthService> _logger;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IValidationService _validationService;

    public DeviceAuthService(
        AuthDbContext db,
        IPublishEndpoint bus,
        ILogger<DeviceAuthService> logger,
        ITokenService tokenService,
        IAuditService auditService,
        IValidationService validationService)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
        _tokenService = tokenService;
        _auditService = auditService;
        _validationService = validationService;
    }

    public async Task<DeviceCredentialResponse> RegisterAsync(DeviceRegisterRequest req, Guid ownerUserId, string? ipAddress = null)
    {
        _logger.LogInformation("Processing device registration for device: {DeviceId}", req.NetworkDeviceId);

        // Validate request
        var (isValid, errorMessage) = await _validationService.ValidateDeviceRegistrationAsync(req, ownerUserId);
        if (!isValid)
        {
            _logger.LogWarning("Device registration validation failed: {ErrorMessage}", errorMessage);
            await _auditService.LogDeviceActionAsync(ownerUserId, req.NetworkDeviceId, "DEVICE_REGISTER", "Failure", ipAddress, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // Generate secret
        var plainSecret = GenerateSecret();
        var secretHash = _hasher.HashPassword("", plainSecret);

        var cred = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            NetworkDeviceId = req.NetworkDeviceId,
            SecretHash = secretHash,
            OwnerUserId = ownerUserId,
            RoomId = req.RoomId,
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _db.DeviceCredentials.Add(cred);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Device registered successfully: {DeviceId} for user: {UserId}", req.NetworkDeviceId, ownerUserId);

        // Publish event and audit log
        await _bus.Publish(new DeviceLinkedEvent(req.NetworkDeviceId, ownerUserId, req.RoomId));
        await _auditService.LogDeviceActionAsync(ownerUserId, req.NetworkDeviceId, "DEVICE_REGISTER", "Success", ipAddress);

        return new DeviceCredentialResponse(req.NetworkDeviceId, plainSecret);
    }

    public async Task<DeviceTokenResponse> AuthenticateAsync(DeviceAuthRequest req, string? ipAddress = null)
    {
        _logger.LogInformation("Processing device authentication for device: {DeviceId}", req.NetworkDeviceId);

        // Validate request
        var (isValid, errorMessage) = _validationService.ValidateDeviceAuth(req);
        if (!isValid)
        {
            _logger.LogWarning("Device authentication validation failed: {ErrorMessage}", errorMessage);
            await _auditService.LogDeviceActionAsync(null, req.NetworkDeviceId, "DEVICE_AUTH", "Failure", ipAddress, errorMessage);
            throw new UnauthorizedAccessException(errorMessage);
        }

        // Find device credentials
        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == req.NetworkDeviceId);

        if (cred is null)
        {
            _logger.LogWarning("Device authentication failed: device not found - {DeviceId}", req.NetworkDeviceId);
            await _auditService.LogDeviceActionAsync(null, req.NetworkDeviceId, "DEVICE_AUTH", "Failure", ipAddress, "Device not found");
            throw new UnauthorizedAccessException("Device not found");
        }

        // Verify secret
        var result = _hasher.VerifyHashedPassword("", cred.SecretHash, req.Secret);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Device authentication failed: invalid secret - {DeviceId}", req.NetworkDeviceId);
            await _auditService.LogDeviceActionAsync(cred.OwnerUserId, req.NetworkDeviceId, "DEVICE_AUTH", "Failure", ipAddress, "Invalid secret");
            throw new UnauthorizedAccessException("Invalid secret");
        }

        // Generate JWT token for device
        var token = _tokenService.GenerateDeviceToken(cred);

        _logger.LogInformation("Device authenticated successfully: {DeviceId}", req.NetworkDeviceId);
        await _auditService.LogDeviceActionAsync(cred.OwnerUserId, req.NetworkDeviceId, "DEVICE_AUTH", "Success", ipAddress);
        
        // Publish event
        await _bus.Publish(new DeviceAuthenticatedEvent(req.NetworkDeviceId, cred.OwnerUserId, DateTime.UtcNow));

        return new DeviceTokenResponse(token, 24 * 60 * 60); // 24 hours in seconds
    }

    public async Task RevokeDeviceAsync(string networkDeviceId, Guid userId)
    {
        _logger.LogInformation("Processing device revocation for device: {DeviceId}", networkDeviceId);

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == networkDeviceId && d.OwnerUserId == userId);

        if (cred is null)
        {
            _logger.LogWarning("Device revocation failed: device not found - {DeviceId}", networkDeviceId);
            throw new InvalidOperationException("Device not found");
        }

        cred.IsRevoked = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Device revoked successfully: {DeviceId}", networkDeviceId);
        await _auditService.LogDeviceActionAsync(userId, networkDeviceId, "DEVICE_REVOKE", "Success");
        
        // Publish event
        await _bus.Publish(new DeviceRevokedEvent(networkDeviceId, userId, DateTime.UtcNow));
    }

    public async Task RotateSecretAsync(string networkDeviceId, Guid userId)
    {
        _logger.LogInformation("Processing device secret rotation for device: {DeviceId}", networkDeviceId);

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == networkDeviceId && d.OwnerUserId == userId);

        if (cred is null)
        {
            _logger.LogWarning("Device secret rotation failed: device not found - {DeviceId}", networkDeviceId);
            throw new InvalidOperationException("Device not found");
        }

        var plainSecret = GenerateSecret();
        cred.SecretHash = _hasher.HashPassword("", plainSecret);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Device secret rotated successfully: {DeviceId}", networkDeviceId);
        
        // Publish event
        await _bus.Publish(new DeviceSecretRotatedEvent(networkDeviceId));
        await _auditService.LogDeviceActionAsync(userId, networkDeviceId, "DEVICE_SECRET_ROTATE", "Success");
    }

    private static string GenerateSecret()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
