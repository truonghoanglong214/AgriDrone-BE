using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionsDbContext(DbContextOptions<MissionsDbContext> options)
    : DbContext(options)
{
    public DbSet<Drone> Drones => Set<Drone>();

    public DbSet<DroneMission> DroneMissions => Set<DroneMission>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MissionMedia> MissionMedia => Set<MissionMedia>();

    public DbSet<AiModelVersion> AiModelVersions => Set<AiModelVersion>();

    public DbSet<AiProcessingJob> AiProcessingJobs => Set<AiProcessingJob>();

    public DbSet<MissionPlantObservation> MissionPlantObservations => Set<MissionPlantObservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mission");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MissionsDbContext).Assembly);
    }
}
