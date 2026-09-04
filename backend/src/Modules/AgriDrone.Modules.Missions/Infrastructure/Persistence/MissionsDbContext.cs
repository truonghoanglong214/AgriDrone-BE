using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionsDbContext(
    DbContextOptions<MissionsDbContext> options)
    : DbContext(options), IMissionsUnitOfWork
{

    private const string MissionScheduleOverlapConstraint =
        "ex_drone_missions_no_schedule_overlap";

    private const string MissionFarmCodeConstraint =
        "uq_drone_missions_farm_code";

    public DbSet<Drone> Drones => Set<Drone>();

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

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }

    public override async Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
            when (exception.Entries.Any(
                entry => entry.Entity is DroneMission))
        {
            throw new MissionConcurrencyException(
                exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState:
                    PostgresErrorCodes.ExclusionViolation,
                ConstraintName:
                    MissionScheduleOverlapConstraint
            })
        {
            throw new MissionScheduleConflictException(
                exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: MissionFarmCodeConstraint
            })
        {
            throw new MissionCodeConflictException(exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mission");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MissionsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
