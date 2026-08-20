using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

public sealed class MappingPublicationDbContext(
    DbContextOptions<MappingPublicationDbContext> options)
    : DbContext(options), IAuditLogSink
{
    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<FarmZone> FarmZones => Set<FarmZone>();

    public DbSet<ZoneMapVersion> ZoneMapVersions => Set<ZoneMapVersion>();

    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantChangeEvent> PlantChangeEvents => Set<PlantChangeEvent>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IgnoreUnrelatedPlantNavigations(modelBuilder);

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmZoneConfiguration());
        modelBuilder.ApplyConfiguration(new ZoneMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConfiguration());
        modelBuilder.ApplyConfiguration(new PlantChangeEventConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        ConfigurePublicationRelationships(modelBuilder);
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
    }
}
