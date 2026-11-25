using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;
using AgriDrone.Database.Mapping;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

public sealed class MappingPublicationDbContext(
    DbContextOptions<MappingPublicationDbContext> options)
    : DbContext(options), IAuditLogSink, IMappingPublicationUnitOfWork
{
    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<FarmZone> FarmZones => Set<FarmZone>();

    public DbSet<FarmBoundary> FarmBoundaries => Set<FarmBoundary>();

    public DbSet<BoundaryException> BoundaryExceptions => Set<BoundaryException>();

    public DbSet<FarmBaseMapVersion> FarmBaseMapVersions =>
        Set<FarmBaseMapVersion>();

    public DbSet<FarmBaselinePointer> FarmBaselinePointers =>
        Set<FarmBaselinePointer>();

    public DbSet<ZoneMapVersion> ZoneMapVersions => Set<ZoneMapVersion>();

    public DbSet<MappingCandidateBatch> MappingCandidateBatches =>
        Set<MappingCandidateBatch>();

    public DbSet<MappingCandidate> MappingCandidates =>
        Set<MappingCandidate>();

    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantProfile> PlantProfiles => Set<PlantProfile>();

    public DbSet<PlantProfileObservation> PlantProfileObservations => Set<PlantProfileObservation>();

    public DbSet<PlantChangeEvent> PlantChangeEvents => Set<PlantChangeEvent>();

    public DbSet<PlantInventoryChangeReport> PlantInventoryChangeReports =>
        Set<PlantInventoryChangeReport>();

    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();

    public DbSet<CaptureAttempt> CaptureAttempts => Set<CaptureAttempt>();

    public DbSet<DroneMission> DroneMissions => Set<DroneMission>();

    public DbSet<AiProcessingJob> AiProcessingJobs => Set<AiProcessingJob>();

    public DbSet<AiModelVersion> AiModelVersions => Set<AiModelVersion>();

    public DbSet<SurveyOrderPoleCountSnapshot> SurveyOrderPoleCountSnapshots =>
        Set<SurveyOrderPoleCountSnapshot>();

    public DbSet<SurveyOrderPoleCountSnapshotItem> SurveyOrderPoleCountSnapshotItems =>
        Set<SurveyOrderPoleCountSnapshotItem>();

    public DbSet<SpatialPolicy> SpatialPolicies => Set<SpatialPolicy>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction =
            await Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PostgreSqlEnumMappings.ConfigureModel(modelBuilder);
        IgnoreUnrelatedPlantNavigations(modelBuilder);

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmZoneConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBoundaryConfiguration());
        modelBuilder.ApplyConfiguration(new BoundaryExceptionConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaseMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaselinePointerConfiguration());
        modelBuilder.ApplyConfiguration(new ZoneMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new MappingCandidateBatchConfiguration());
        modelBuilder.ApplyConfiguration(new MappingCandidateConfiguration());
        modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConfiguration());
        modelBuilder.ApplyConfiguration(new PlantProfileConfiguration());
        modelBuilder.ApplyConfiguration(new PlantProfileObservationConfiguration());
        modelBuilder.ApplyConfiguration(new PlantChangeEventConfiguration());
        modelBuilder.ApplyConfiguration(
            new PlantInventoryChangeReportConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new CaptureAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new DroneConfiguration());
        modelBuilder.ApplyConfiguration(new DroneMissionConfiguration());
        modelBuilder.ApplyConfiguration(new AiModelVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AiProcessingJobConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderPoleCountSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new SpatialPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        ConfigurePublicationRelationships(modelBuilder);
        RemoveSurveyOrderDependencies(modelBuilder);
    }

    private static void RemoveSurveyOrderDependencies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SurveyOrder>()
            .Ignore(order => order.Appointments)
            .Ignore(order => order.Payments);
        modelBuilder.Ignore<SurveyRequest>();
        modelBuilder.Ignore<SurveyService>();
        modelBuilder.Ignore<SurveyServicePrice>();
    }

    private static void IgnoreUnrelatedPlantNavigations(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plant>()
            .Ignore(plant => plant.Scans);

        modelBuilder.Entity<HealthLevel>()
            .Ignore(level => level.PlantScans)
            .Ignore(level => level.ConditionDetections)
            .Ignore(level => level.CorrectedScanVerifications)
            .Ignore(level => level.CorrectedDetectionReviews);

        modelBuilder.Entity<DroneMission>()
            .Ignore(mission => mission.Media)
            .Ignore(mission => mission.TelemetryPoints)
            .Ignore(mission => mission.PlantObservations)
            .Ignore(mission => mission.PreflightChecklists);

        modelBuilder.Entity<AiProcessingJob>()
            .Ignore(job => job.ThresholdProfile)
            .Ignore(job => job.PlantObservations);

        modelBuilder.Entity<AiModelVersion>()
            .Ignore(model => model.ThresholdProfiles)
            .Ignore(model => model.PlantObservations);
    }

    private static void ConfigurePublicationRelationships(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plant>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(plant => plant.FarmId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_farms_farm_id");

        modelBuilder.Entity<Plant>()
            .HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(plant => new { plant.ZoneId, plant.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_zone_same_farm");

        modelBuilder.Entity<Plant>()
            .HasOne<ZoneMapVersion>()
            .WithMany()
            .HasForeignKey(plant => new
            {
                plant.CurrentMapVersionId,
                plant.ZoneId,
                plant.FarmId
            })
            .HasPrincipalKey(mapVersion => new
            {
                mapVersion.Id,
                mapVersion.ZoneId,
                mapVersion.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_current_map_version_same_zone");

        modelBuilder.Entity<Plant>()
            .HasOne<FarmBaseMapVersion>()
            .WithMany()
            .HasForeignKey(plant => new
            {
                plant.FarmBaseMapVersionId,
                plant.FarmId
            })
            .HasPrincipalKey(mapVersion => new
            {
                mapVersion.Id,
                mapVersion.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_farm_base_map_version_same_farm");

        modelBuilder.Entity<AuditLog>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(auditLog => new
            {
                auditLog.FarmId,
                auditLog.TenantId
            })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_farms_same_tenant");

        modelBuilder.Entity<SurveyOrder>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(order => new { order.FarmId, order.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_farms_same_tenant");

        modelBuilder.Entity<FarmBaseMapVersion>()
            .HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(map => new
            {
                map.SourceSurveyOrderId,
                map.TenantId,
                map.FarmId
            })
            .HasPrincipalKey(order => new
            {
                order.Id,
                order.TenantId,
                order.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_farm_base_map_versions_orders_same_tenant_farm");
    }
}
