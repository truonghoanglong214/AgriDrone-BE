using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

public sealed class SurveyComboImportDbContext(
    DbContextOptions<SurveyComboImportDbContext> options)
    : DbContext(options), IAuditLogSink
{
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<CaptureAttempt> CaptureAttempts => Set<CaptureAttempt>();
    public DbSet<SurveyOrderCommercialSnapshot> SurveyOrderCommercialSnapshots => Set<SurveyOrderCommercialSnapshot>();
    public DbSet<SurveyComboImportBatch> SurveyComboImportBatches => Set<SurveyComboImportBatch>();
    public DbSet<CaptureInputManifest> CaptureInputManifests => Set<CaptureInputManifest>();
    public DbSet<CaptureInputApproval> CaptureInputApprovals => Set<CaptureInputApproval>();
    public DbSet<CaptureInputManifestFile> CaptureInputManifestFiles => Set<CaptureInputManifestFile>();

    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<FarmBoundary> FarmBoundaries => Set<FarmBoundary>();
    public DbSet<FarmBaseMapVersion> FarmBaseMapVersions => Set<FarmBaseMapVersion>();
    public DbSet<MappingCandidateBatch> MappingCandidateBatches => Set<MappingCandidateBatch>();
    public DbSet<MappingCandidate> MappingCandidates => Set<MappingCandidate>();

    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<PlantCondition> PlantConditions => Set<PlantCondition>();
    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

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
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new CaptureAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderCommercialSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyComboImportBatchConfiguration());
        modelBuilder.ApplyConfiguration(new CaptureInputManifestConfiguration());
        modelBuilder.ApplyConfiguration(new CaptureInputApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new CaptureInputManifestFileConfiguration());

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBoundaryConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaseMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new MappingCandidateBatchConfiguration());
        modelBuilder.ApplyConfiguration(new MappingCandidateConfiguration());

        modelBuilder.ApplyConfiguration(new PlantConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConditionConfiguration());
        modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());

        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        IgnoreUnrelatedNavigations(modelBuilder);
    }

    private static void IgnoreUnrelatedNavigations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SurveyOrder>()
            .Ignore(order => order.Appointments)
            .Ignore(order => order.Payments)
            .Ignore(order => order.ZoneScopes);

        modelBuilder.Entity<CaptureAttempt>()
            .Ignore(attempt => attempt.History);

        modelBuilder.Entity<Plant>()
            .Ignore(plant => plant.Scans)
            .Ignore(plant => plant.ChangeEvents);

        modelBuilder.Entity<PlantCondition>()
            .Ignore(condition => condition.Detections)
            .Ignore(condition => condition.CorrectedReviews);

        modelBuilder.Entity<HealthLevel>()
            .Ignore(level => level.PlantScans)
            .Ignore(level => level.ConditionDetections)
            .Ignore(level => level.CorrectedScanVerifications)
            .Ignore(level => level.CorrectedDetectionReviews);

        modelBuilder.Ignore<SurveyRequest>();
        modelBuilder.Ignore<SurveyService>();
        modelBuilder.Ignore<SurveyServicePrice>();
        modelBuilder.Ignore<SurveyResult>();
    }
}
