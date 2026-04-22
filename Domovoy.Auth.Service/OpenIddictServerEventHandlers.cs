using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;

namespace Domovoy.Auth.Service;

/// <summary>
/// OpenIddict server event handler for processing token requests
/// </summary>
public class OpenIddictServerEventHandlers
{
    private readonly ILogger<OpenIddictServerEventHandlers> _logger;
    private readonly UserManager<AuthUser> _userManager;

    public OpenIddictServerEventHandlers(
        ILogger<OpenIddictServerEventHandlers> logger,
        UserManager<AuthUser> userManager)
    {
        _logger = logger;
        _userManager = userManager;
    }

    /// <summary>
    /// Process token requests and create claims
    /// </summary>
    public async Task OnValidateTokenRequestAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (!context.Request.IsPasswordGrantType())
            return;

        _logger.LogInformation("🔑 Validating password grant request");

        // Find user by email or username
        var user = await _userManager.FindByNameAsync(context.Request.Username ?? string.Empty)
                   ?? await _userManager.FindByEmailAsync(context.Request.Username ?? string.Empty);

        if (user is null)
        {
            _logger.LogWarning("❌ User not found: {Username}", context.Request.Username);
            return;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("❌ User is inactive: {UserId}", user.Id);
            return;
        }

        // Validate password
        if (!await _userManager.CheckPasswordAsync(user, context.Request.Password ?? string.Empty))
        {
            _logger.LogWarning("❌ Invalid password for user: {UserId}", user.Id);
            return;
        }

        context.Principal = await CreatePrincipalAsync(user);
        _logger.LogInformation("✅ Token validation succeeded for user: {UserId}", user.Id);
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(AuthUser user)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Email ?? user.Id.ToString());

        if (!string.IsNullOrWhiteSpace(user.Email))
            identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email);

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);

        var principal = new ClaimsPrincipal(identity);

        foreach (var claim in principal.Claims)
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);

        return principal;
    }
}
