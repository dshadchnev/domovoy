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

            if (await manager.FindByClientIdAsync("domovoy-client", cancellationToken) == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "domovoy-client",
                    DisplayName = "Domovoy Smart Home Client",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.Password,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddictConstants.Scopes.OpenId,
                        OpenIddictConstants.Scopes.Profile
                    }
                };
                await manager.CreateAsync(descriptor, cancellationToken);
                _logger.LogInformation("✅ Client 'domovoy-client' registered");
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