using AgriDrone.Modules.Plants.Domain.Diseases;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence;

internal sealed class PlantsDbContext(DbContextOptions<PlantsDbContext> options)
    : DbContext(options)
{
    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantChangeEvent> PlantChangeEvents => Set<PlantChangeEvent>();

    public DbSet<Disease> Diseases => Set<Disease>();

    public DbSet<PlantScan> PlantScans => Set<PlantScan>();

    public DbSet<PlantScanMedia> PlantScanMedia => Set<PlantScanMedia>();

    public DbSet<DiseaseDetection> DiseaseDetections => Set<DiseaseDetection>();

    public DbSet<DiseaseLesion> DiseaseLesions => Set<DiseaseLesion>();

    public DbSet<ScanVerification> ScanVerifications => Set<ScanVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("plant");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantsDbContext).Assembly);
    }
}
