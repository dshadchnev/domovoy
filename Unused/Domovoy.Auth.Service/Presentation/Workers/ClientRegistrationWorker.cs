using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Domovoy.Auth.Service.Presentation.Workers;

/// <summary>
/// Registers OpenIddict OAuth clients on startup.
/// Fails fast in Production if registration fails вЂ” prevents silent auth breakage.
/// </summary>
public class ClientRegistrationWorker(
    IServiceProvider sp,
    ILogger<ClientRegistrationWorker> logger,
    IConfiguration config,
    IWebHostEnvironment env) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                attempt++;
                // Small delay to allow DB migrations to complete first
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), cancellationToken);

                await RegisterClientsAsync(cancellationToken);
                logger.LogInformation("вњ… OpenIddict clients registered successfully on attempt {Attempt}", attempt);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("ClientRegistrationWorker cancelled during startup.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex,
                    "ClientRegistrationWorker attempt {Attempt}/{Max} failed. Retrying...",
                    attempt, maxRetries);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "ClientRegistrationWorker failed after {Max} attempts. OpenIddict token introspection will NOT work.",
                    maxRetries);

                // In Production: fail fast so the issue is visible immediately
                if (env.IsProduction())
                    throw new InvalidOperationException(
                        "Failed to register OpenIddict clients. See inner exception.", ex);

                // In Development: log and continue (developer may not have DB running)
                logger.LogWarning("Continuing without OpenIddict client registration (Development mode).");
                return;
            }
        }
    }

    private async Task RegisterClientsAsync(CancellationToken cancellationToken)
    {
        using var scope = sp.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        // Public UI client
        await UpsertClientAsync(manager, new OpenIddictApplicationDescriptor
        {
            ClientId = "domovoy-client",
            DisplayName = "Domovoy Smart Home Client",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile
            }
        }, cancellationToken);

        // Device Manager вЂ” introspection client
        var deviceMgrSecret = config["OpenIddict:ClientSecrets:DeviceManager"]
            ?? config["OPENIDDICT_DEVICEMGR_SECRET"]
            ?? throw new InvalidOperationException(
                "OpenIddict client secret for DeviceManager not configured. " +
                "Set OpenIddict__ClientSecrets__DeviceManager or OPENIDDICT_DEVICEMGR_SECRET.");

        await UpsertClientAsync(manager, new OpenIddictApplicationDescriptor
        {
            ClientId = "domovoy-device-manager",
            ClientSecret = deviceMgrSecret,
            DisplayName = "Domovoy Device Manager Service",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions = { OpenIddictConstants.Permissions.Endpoints.Introspection }
        }, cancellationToken);

        // Rules Engine вЂ” introspection client
        var rulesSecret = config["OpenIddict:ClientSecrets:RulesEngine"]
            ?? config["OPENIDDICT_RULES_SECRET"]
            ?? throw new InvalidOperationException(
                "OpenIddict client secret for RulesEngine not configured. " +
                "Set OpenIddict__ClientSecrets__RulesEngine or OPENIDDICT_RULES_SECRET.");

        await UpsertClientAsync(manager, new OpenIddictApplicationDescriptor
        {
            ClientId = "domovoy-rules-engine",
            ClientSecret = rulesSecret,
            DisplayName = "Domovoy Rules Engine Service",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions = { OpenIddictConstants.Permissions.Endpoints.Introspection }
        }, cancellationToken);
    }

    private async Task UpsertClientAsync(
        IOpenIddictApplicationManager manager,
        OpenIddictApplicationDescriptor descriptor,
        CancellationToken ct)
    {
        var existing = await manager.FindByClientIdAsync(descriptor.ClientId!, ct);
        if (existing == null)
        {
            await manager.CreateAsync(descriptor, ct);
            logger.LogInformation("вњ… OpenIddict client '{ClientId}' created", descriptor.ClientId);
        }
        else
        {
            await manager.UpdateAsync(existing, descriptor, ct);
            logger.LogInformation("рџ”„ OpenIddict client '{ClientId}' updated", descriptor.ClientId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}