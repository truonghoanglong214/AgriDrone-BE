using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionsDbContext(
    DbContextOptions<MissionsDbContext> options)
    : DbContext(options), IMissionsUnitOfWork
{
    public DbSet<Drone> Drones => Set<Drone>();

    public DbSet<DroneStatusChange> DroneStatusChanges =>
        Set<DroneStatusChange>();

    public DbSet<DroneMission> DroneMissions =>
        Set<DroneMission>();

    public DbSet<MediaAsset> MediaAssets =>
        Set<MediaAsset>();

    public DbSet<MissionMedia> MissionMedia =>
        Set<MissionMedia>();

    public DbSet<AiModelVersion> AiModelVersions =>
        Set<AiModelVersion>();

    public DbSet<AiProcessingJob> AiProcessingJobs =>
        Set<AiProcessingJob>();

    public DbSet<AiThresholdProfile> AiThresholdProfiles =>
        Set<AiThresholdProfile>();

    public DbSet<AiDetectionThreshold> AiDetectionThresholds =>
        Set<AiDetectionThreshold>();

    public DbSet<MissionPlantObservation>
        MissionPlantObservations =>
            Set<MissionPlantObservation>();

    public DbSet<ObservationMatchCandidate>
        ObservationMatchCandidates =>
            Set<ObservationMatchCandidate>();

    public DbSet<MissionTelemetryPoint>
        MissionTelemetryPoints =>
            Set<MissionTelemetryPoint>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mission");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MissionsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
