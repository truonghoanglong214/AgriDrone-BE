using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence;

internal sealed class FarmsDbContext(DbContextOptions<FarmsDbContext> options)
    : DbContext(options), IFarmUnitOfWork
{
    public DbSet<Farm> Farms => Set<Farm>();

    public DbSet<FarmZone> FarmZones => Set<FarmZone>();

    public DbSet<ZoneMapVersion> ZoneMapVersions => Set<ZoneMapVersion>();

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
        modelBuilder.HasDefaultSchema("farm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarmsDbContext).Assembly);

        modelBuilder.ApplyConfiguration(
            new AuditLogConfiguration());

        modelBuilder.Entity<AuditLog>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(auditLog => new
            {
                auditLog.FarmId,
                auditLog.TenantId
            })
            .HasPrincipalKey(farm => new
            {
                farm.Id,
                farm.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_farms_same_tenant");
    }
}
