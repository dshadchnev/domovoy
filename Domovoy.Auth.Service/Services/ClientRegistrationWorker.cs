using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Domovoy.Auth.Service.Services;

public class ClientRegistrationWorker(IServiceProvider sp, ILogger<ClientRegistrationWorker> logger) : IHostedService
{
    private readonly IServiceProvider _sp = sp;
    private readonly ILogger<ClientRegistrationWorker> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            using var scope = _sp.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            var clientDescriptor = new OpenIddictApplicationDescriptor
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
            };

            var client = await manager.FindByClientIdAsync("domovoy-client", cancellationToken);
            if (client == null)
            {
                await manager.CreateAsync(clientDescriptor, cancellationToken);
                _logger.LogInformation("✅ Client 'domovoy-client' registered");
            }
            else
            {
                await manager.UpdateAsync(client, clientDescriptor, cancellationToken);
                _logger.LogInformation("✅ Client 'domovoy-client' updated");
            }

            // Регистрация Device Manager как клиента introspection
            var introspectionDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = "domovoy-device-manager",
                ClientSecret = "device-manager-secret",
                DisplayName = "Domovoy Device Manager Service",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Introspection
                }
            };

            var introspectionClient = await manager.FindByClientIdAsync("domovoy-device-manager", cancellationToken);
            if (introspectionClient == null)
            {
                await manager.CreateAsync(introspectionDescriptor, cancellationToken);
                _logger.LogInformation("✅ Client 'domovoy-device-manager' registered for introspection");
            }
            else
            {
                await manager.UpdateAsync(introspectionClient, introspectionDescriptor, cancellationToken);
                _logger.LogInformation("✅ Client 'domovoy-device-manager' updated for introspection");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ ClientRegistrationWorker failed to initialize. OpenIddict features may not be available.");
            // Don't throw - let the service continue without OpenIddict client registration
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}