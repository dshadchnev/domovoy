using Microsoft.EntityFrameworkCore;

namespace Domovoy.RulesEngine.Service.Data;

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
        });
    }
}