using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence;

internal sealed class PlantsDbContext(DbContextOptions<PlantsDbContext> options)
    : DbContext(options), IPlantsUnitOfWork
{
    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<PlantChangeEvent> PlantChangeEvents => Set<PlantChangeEvent>();

    public DbSet<PlantCondition> PlantConditions => Set<PlantCondition>();

    public DbSet<HealthLevel> HealthLevels => Set<HealthLevel>();

    public DbSet<PlantScan> PlantScans => Set<PlantScan>();

    public DbSet<PlantScanMedia> PlantScanMedia => Set<PlantScanMedia>();

    public DbSet<ConditionDetection> ConditionDetections => Set<ConditionDetection>();

    public DbSet<ConditionLesion> ConditionLesions => Set<ConditionLesion>();

    public DbSet<ScanVerification> ScanVerifications => Set<ScanVerification>();

    public DbSet<ConditionDetectionReview> ConditionDetectionReviews =>
        Set<ConditionDetectionReview>();

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
        await using var transaction =
            await Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("plant");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
