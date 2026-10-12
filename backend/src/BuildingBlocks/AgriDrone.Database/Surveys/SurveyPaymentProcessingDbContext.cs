using System.Data;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

public sealed class SurveyPaymentProcessingDbContext(
    DbContextOptions<SurveyPaymentProcessingDbContext> options)
    : DbContext(options), ISurveyPaymentProcessingUnitOfWork
{
    private bool _transactionActive;

    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<SurveyPayment> SurveyPayments => Set<SurveyPayment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PriceAdjustment> PriceAdjustments => Set<PriceAdjustment>();
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
                "A payment-processing transaction is already active.");
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
            "Payment processing can only persist through ExecuteInTransactionAsync.");

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
                "Payment persistence requires the active specialized transaction.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SurveyOrderConfiguration());
        modelBuilder.ApplyConfiguration(new SurveyPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentEventConfiguration());
        modelBuilder.ApplyConfiguration(new PriceAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.Entity<SurveyOrder>()
            .Ignore(order => order.SurveyRequest)
            .Ignore(order => order.SurveyService)
            .Ignore(order => order.SurveyServicePrice)
            .Ignore(order => order.PreviousCompatibleOrder)
            .Ignore(order => order.Appointments);
        modelBuilder.Ignore<SurveyRequest>();
        modelBuilder.Ignore<SurveyService>();
        modelBuilder.Ignore<SurveyServicePrice>();
        modelBuilder.Ignore<SurveyAppointment>();
    }
}
