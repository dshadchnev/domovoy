using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domovoy.CommandDispatcher.Service.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Uses local connection string so migrations can be created without real config.
/// </summary>
public class DispatcherDbContextFactory : IDesignTimeDbContextFactory<DispatcherDbContext>
{
    public DispatcherDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DispatcherDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=domovoy_auth;Username=postgres;Password=postgres");

        return new DispatcherDbContext(optionsBuilder.Options);
    }
}
