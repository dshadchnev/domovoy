using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Services;
using Domovoy.Auth.Service.Data.Entities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Domovoy.Auth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IUserAuthService _userAuthService;
    private readonly ILogger<AuthController> _logger;
    private readonly UserManager<AuthUser> _userManager;
    private readonly RoleManager<AuthRole> _roleManager;

    public AuthController(
        IUserAuthService userAuthService, 
        ILogger<AuthController> logger,
        UserManager<AuthUser> userManager,
        RoleManager<AuthRole> roleManager)
    {
        _userAuthService = userAuthService;
        _logger = logger;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [AllowAnonymous]
    [HttpPost("token", Name = "TokenEndpoint")]
    [Route("/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> TokenEndpoint()
    {
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>();
        logger.LogInformation("🔑 [TokenEndpoint] Request received");
        
        var request = HttpContext.GetOpenIddictServerRequest();
        logger.LogInformation("🔑 [TokenEndpoint] OpenIddict request: {GrantType}", request?.GrantType ?? "null");
        
        if (request is null)
        {
            logger.LogWarning("🔑 [TokenEndpoint] Request is null!");
            return BadRequest(new { error = "Invalid request" });
        }

        if (!request.IsPasswordGrantType())
        {
            logger.LogWarning("🔑 [TokenEndpoint] Not password grant: {GrantType}", request.GrantType);
            return BadRequest(new { error = "Unsupported grant type" });
        }

        var username = request.Username ?? string.Empty;
        logger.LogInformation("🔑 [TokenEndpoint] Finding user: {Username}", username);
        
        var user = await _userManager.FindByNameAsync(username)
                   ?? await _userManager.FindByEmailAsync(username);

        if (user is null)
        {
            logger.LogWarning("🔑 [TokenEndpoint] User not found: {Username}", username);
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            logger.LogWarning("🔑 [TokenEndpoint] User inactive: {UserId}", user.Id);
            return Unauthorized();
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password ?? string.Empty);
        logger.LogInformation("🔑 [TokenEndpoint] Password valid: {Valid}", passwordValid);
        
        if (!passwordValid)
        {
            logger.LogWarning("🔑 [TokenEndpoint] Invalid password: {UserId}", user.Id);
            return Unauthorized();
        }

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
        principal.SetScopes(request.GetScopes());

        foreach (var claim in principal.Claims)
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);

        logger.LogInformation("🔑 [TokenEndpoint] SUCCESS - Token granted: {UserId}", user.Id);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedAdmin()
    {
        // Create role if not exists
        if (!await _roleManager.RoleExistsAsync("Admin"))
            await _roleManager.CreateAsync(new AuthRole { Name = "Admin", Description = "Administrator" });

        // Create admin user if not exists
        var email = "admin@domovoy.local";
        if (await _userManager.FindByEmailAsync(email) == null)
        {
            var user = new AuthUser 
            { 
                UserName = email, 
                Email = email, 
                FirstName = "Admin", 
                LastName = "User",
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, "StrongPass123!");
            if (result.Succeeded) 
                await _userManager.AddToRoleAsync(user, "Admin");
            else
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        return Ok(new { message = "Admin user seeded", email });
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] UserRegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _userAuthService.RegisterAsync(request, GetClientIp());
            return CreatedAtAction(nameof(Register), new { userId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _userAuthService.LoginAsync(request, GetClientIp());
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Failed login attempt: {Message}", ex.Message);
            return Unauthorized(new { error = "Invalid username or password" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "An error occurred during login" });
        }
    }

    private string GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}
