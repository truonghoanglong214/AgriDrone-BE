using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using AgriDrone.SharedInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence;

internal sealed class SurveysDbContext(DbContextOptions<SurveysDbContext> options)
    : DbContext(options), ISurveysUnitOfWork
{
    public DbSet<SurveyService> SurveyServices => Set<SurveyService>();
    public DbSet<SurveyServicePrice> SurveyServicePrices => Set<SurveyServicePrice>();
    public DbSet<SurveyRequest> SurveyRequests => Set<SurveyRequest>();
    public DbSet<SurveyRequestReview> SurveyRequestReviews => Set<SurveyRequestReview>();
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<SurveyAppointment> SurveyAppointments => Set<SurveyAppointment>();
    public DbSet<SurveyPayment> SurveyPayments => Set<SurveyPayment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PriceAdjustment> PriceAdjustments => Set<PriceAdjustment>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<HarvestReadinessAssessment> HarvestReadinessAssessments => Set<HarvestReadinessAssessment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
        modelBuilder.HasDefaultSchema("survey");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveysDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
