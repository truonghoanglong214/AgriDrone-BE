using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
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

    public DbSet<AiThresholdProfile> AiThresholdProfiles => Set<AiThresholdProfile>();

    public DbSet<AiDetectionThreshold> AiDetectionThresholds => Set<AiDetectionThreshold>();

    public DbSet<MissionPlantObservation> MissionPlantObservations => Set<MissionPlantObservation>();

    public DbSet<ObservationMatchCandidate> ObservationMatchCandidates =>
        Set<ObservationMatchCandidate>();

    public DbSet<MissionTelemetryPoint> MissionTelemetryPoints => Set<MissionTelemetryPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mission");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MissionsDbContext).Assembly);
    }
}
