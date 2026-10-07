using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

public sealed class SurveyResultPublicationDbContext(
    DbContextOptions<SurveyResultPublicationDbContext> options)
    : DbContext(options), IAuditLogSink, ISurveyResultPublicationUnitOfWork
{
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();

    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantCondition> PlantConditions => Set<PlantCondition>();

    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

    public DbSet<DiseaseZone> DiseaseZones => Set<DiseaseZone>();

    public DbSet<DiseaseZonePlantMembership> DiseaseZoneMemberships =>
        Set<DiseaseZonePlantMembership>();

    public DbSet<TreatmentRecommendation> TreatmentRecommendations =>
        Set<TreatmentRecommendation>();

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
        IgnoreUnrelatedNavigations(modelBuilder);

        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyResultConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConfiguration());
        modelBuilder.ApplyConfiguration(new PlantConditionConfiguration());
        modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());
        modelBuilder.ApplyConfiguration(new DiseaseZoneConfiguration());
        modelBuilder.ApplyConfiguration(
            new DiseaseZonePlantMembershipConfiguration());
        modelBuilder.ApplyConfiguration(
            new TreatmentRecommendationConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        RemoveSurveyOrderDependencies(modelBuilder);
    }

    private static void IgnoreUnrelatedNavigations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SurveyResult>()
            .Ignore(result => result.HarvestReadinessAssessments);

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
}
