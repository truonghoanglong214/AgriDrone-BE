using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
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

    private const string TelemetryImportOperationConstraint =
    "uq_telemetry_imports_operation";

    private const string TelemetryImportMissionConstraint =
        "uq_telemetry_imports_mission";

    private const string TelemetrySequenceConstraint =
        "uq_mission_telemetry_mission_sequence";

    private const string TelemetryRecordedAtConstraint =
        "uq_mission_telemetry_mission_recorded_at";

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

    public DbSet<MissionTelemetryImport> MissionTelemetryImports => Set<MissionTelemetryImport>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }
    public DbSet<MediaUploadSession> MediaUploadSessions => Set<MediaUploadSession>();
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
        catch (DbUpdateConcurrencyException exception)
            when (exception.Entries.Any(
        entry => entry.Entity is MediaUploadSession))
        {
            throw new UploadSessionConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_upload_sessions_operation"
            })
        {
            throw new UploadOperationConflictException(exception);
        }

        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName:
                    TelemetryImportOperationConstraint or
                    TelemetryImportMissionConstraint or
                    TelemetrySequenceConstraint or
                    TelemetryRecordedAtConstraint
            })
        {
            throw new TelemetryImportConflictException(
                exception);
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
