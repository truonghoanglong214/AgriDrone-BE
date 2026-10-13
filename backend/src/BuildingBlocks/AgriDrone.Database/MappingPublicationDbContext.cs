using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
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

    public DbSet<ZoneMapVersion> ZoneMapVersions => Set<ZoneMapVersion>();

    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantChangeEvent> PlantChangeEvents => Set<PlantChangeEvent>();

    public DbSet<PlantInventoryChangeReport> PlantInventoryChangeReports =>
        Set<PlantInventoryChangeReport>();

    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    internal DbSet<MissionPublicationState> MissionPublicationStates =>
        Set<MissionPublicationState>();

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
        IgnoreUnrelatedPlantNavigations(modelBuilder);

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmZoneConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBoundaryConfiguration());
        modelBuilder.ApplyConfiguration(new BoundaryExceptionConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaseMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ZoneMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConfiguration());
        modelBuilder.ApplyConfiguration(new PlantChangeEventConfiguration());
        modelBuilder.ApplyConfiguration(
            new PlantInventoryChangeReportConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(
            new MissionPublicationStateConfiguration());

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
