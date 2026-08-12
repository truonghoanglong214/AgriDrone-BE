using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence;

internal sealed class HarvestsDbContext(DbContextOptions<HarvestsDbContext> options)
    : DbContext(options)
{
    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<HarvestBatch> HarvestBatches => Set<HarvestBatch>();

    public DbSet<HarvestQualityGrade> HarvestQualityGrades => Set<HarvestQualityGrade>();

    public DbSet<PlantHarvestRecord> PlantHarvestRecords => Set<PlantHarvestRecord>();

    public DbSet<PlantHarvestQualityDetail> PlantHarvestQualityDetails =>
        Set<PlantHarvestQualityDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("harvest");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HarvestsDbContext).Assembly);
    }
}
