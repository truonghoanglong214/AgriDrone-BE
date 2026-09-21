using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence;

internal sealed class HarvestsDbContext(DbContextOptions<HarvestsDbContext> options)
    : DbContext(options), IHarvestsUnitOfWork
{
    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<HarvestBatch> HarvestBatches => Set<HarvestBatch>();

    public DbSet<HarvestQualityGrade> HarvestQualityGrades => Set<HarvestQualityGrade>();

    public DbSet<PlantHarvestRecord> PlantHarvestRecords => Set<PlantHarvestRecord>();

    public DbSet<PlantHarvestQualityDetail> PlantHarvestQualityDetails =>
        Set<PlantHarvestQualityDetail>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await using var transaction =
        await Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("harvest");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HarvestsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
