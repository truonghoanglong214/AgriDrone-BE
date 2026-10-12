using System.Data;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// Atomic persistence boundary for FarmBoundary approval and SurveyOrder scope
/// verification. It intentionally maps only the Phase 4A graph.
/// </summary>
public sealed class SurveyBoundaryVerificationDbContext(
    DbContextOptions<SurveyBoundaryVerificationDbContext> options)
    : DbContext(options), ISurveyBoundaryVerificationUnitOfWork
{
    private bool _transactionActive;

    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<FarmZone> FarmZones => Set<FarmZone>();
    public DbSet<FarmBoundary> FarmBoundaries => Set<FarmBoundary>();
    public DbSet<FarmBaseMapVersion> FarmBaseMapVersions =>
        Set<FarmBaseMapVersion>();
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        EnsureTransaction();
        AuditLogs.Add(auditLog);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_transactionActive || Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A survey boundary-verification transaction is already active.");
        }

        await using var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        _transactionActive = true;

        try
        {
            var result = await operation(cancellationToken);
            if (result is Result { IsFailure: true })
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }

            await base.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _transactionActive = false;
            ChangeTracker.Clear();
        }
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Survey boundary verification can only persist through ExecuteInTransactionAsync.");

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        return base.SaveChangesAsync(cancellationToken);
    }

    internal void AddOutboxMessage(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureTransaction();
        OutboxMessages.Add(message);
    }

    internal void EnsureTransaction()
    {
        if (!_transactionActive || Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Survey boundary persistence requires the active specialized transaction.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FarmZoneConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBoundaryConfiguration());
        modelBuilder.ApplyConfiguration(new FarmBaseMapVersionConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.Entity<SurveyOrder>()
            .Ignore(order => order.SurveyRequest)
            .Ignore(order => order.SurveyService)
            .Ignore(order => order.SurveyServicePrice)
            .Ignore(order => order.PreviousCompatibleOrder)
            .Ignore(order => order.Appointments)
            .Ignore(order => order.Payments);
        modelBuilder.Entity<FarmBaseMapVersion>()
            .Ignore(map => map.ZoneMapVersions);

        modelBuilder.Ignore<SurveyRequest>();
        modelBuilder.Ignore<SurveyService>();
        modelBuilder.Ignore<SurveyServicePrice>();
        modelBuilder.Ignore<SurveyAppointment>();
        modelBuilder.Ignore<SurveyPayment>();
    }
}
