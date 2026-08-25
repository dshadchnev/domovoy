using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Domovoy.RulesEngine.Service.Infrastructure.Persistence;

public class RulesEngineDbContext : DbContext
{
    public RulesEngineDbContext(DbContextOptions<RulesEngineDbContext> options) : base(options) { }

    public DbSet<Rule> Rules => Set<Rule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rule>(entity =>
        {
            entity.HasIndex(r => new { r.DeviceId, r.IsActive });
            entity.HasIndex(r => r.UserId);
            entity.Property(r => r.SensorDeviceId).IsRequired(false);
            entity.Property(r => r.ActuatorDeviceId).IsRequired(false);
        });
    }
}

/// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: Design-time factory for EF migrations.
/// РџРµСЂРµРІРµРґРµРЅРѕ: Allows 'dotnet ef migrations add ...' without running the full app
/// РџРµСЂРµРІРµРґРµРЅРѕ: (which fails because OpenIddict requires a configured client identifier).
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
public class RulesEngineDbContextFactory : IDesignTimeDbContextFactory<RulesEngineDbContext>
{
    public RulesEngineDbContext CreateDbContext(string[] args)
    {
        // РџРµСЂРµРІРµРґРµРЅРѕ: Try to read from appsettings.json, fall back to local dev connection string
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=domovoy_auth;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<RulesEngineDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new RulesEngineDbContext(optionsBuilder.Options);
    }
}
