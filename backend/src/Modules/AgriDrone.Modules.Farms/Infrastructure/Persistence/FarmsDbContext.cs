using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence;

internal sealed class FarmsDbContext(DbContextOptions<FarmsDbContext> options)
    : DbContext(options)
{
    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<FarmZone> FarmZones => Set<FarmZone>();

    public DbSet<ZoneMapVersion> ZoneMapVersions => Set<ZoneMapVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("farm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarmsDbContext).Assembly);
    }
}
