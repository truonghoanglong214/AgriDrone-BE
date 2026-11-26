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
    public DbSet<LegacySurveyDepositPolicy> LegacySurveyDepositPolicies => Set<LegacySurveyDepositPolicy>();
    public DbSet<SurveyComboDiscountPolicy> SurveyComboDiscountPolicies => Set<SurveyComboDiscountPolicy>();
    public DbSet<SurveyRequest> SurveyRequests => Set<SurveyRequest>();
    public DbSet<SurveyRequestReview> SurveyRequestReviews => Set<SurveyRequestReview>();
    public DbSet<SurveyApprovalIdempotencyRecord>
        SurveyApprovalIdempotencyRecords =>
        Set<SurveyApprovalIdempotencyRecord>();
    public DbSet<SurveyOrder> SurveyOrders => Set<SurveyOrder>();
    public DbSet<SurveyOrderZoneScope> SurveyOrderZoneScopes =>
        Set<SurveyOrderZoneScope>();
    public DbSet<SurveyAppointment> SurveyAppointments => Set<SurveyAppointment>();
    public DbSet<BoundaryCaptureSession> BoundaryCaptureSessions =>
        Set<BoundaryCaptureSession>();
    public DbSet<BoundaryCaptureSample> BoundaryCaptureSamples =>
        Set<BoundaryCaptureSample>();
    public DbSet<BaselineRoutePlanReference> BaselineRoutePlanReferences =>
        Set<BaselineRoutePlanReference>();
    public DbSet<SurveyPayment> SurveyPayments => Set<SurveyPayment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PriceAdjustment> PriceAdjustments => Set<PriceAdjustment>();
    public DbSet<ScopeAdjustment> ScopeAdjustments => Set<ScopeAdjustment>();
    public DbSet<AdjustmentSettlement> AdjustmentSettlements =>
        Set<AdjustmentSettlement>();
    public DbSet<ExtraFlightRequest> ExtraFlightRequests =>
        Set<ExtraFlightRequest>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<QualityRefundCase> QualityRefundCases => Set<QualityRefundCase>();
    public DbSet<HarvestReadinessAssessment> HarvestReadinessAssessments => Set<HarvestReadinessAssessment>();
    public DbSet<HarvestReadinessCriterion> HarvestReadinessCriteria => Set<HarvestReadinessCriterion>();
    public DbSet<SurveyServiceRevision> SurveyServiceRevisions => Set<SurveyServiceRevision>();
    public DbSet<SurveyDepositPolicy> SurveyDepositPolicies => Set<SurveyDepositPolicy>();
    public DbSet<ClaimPolicy> ClaimPolicies => Set<ClaimPolicy>();
    public DbSet<CancellationPolicy> CancellationPolicies => Set<CancellationPolicy>();
    public DbSet<BusinessPolicyVersion> BusinessPolicyVersions => Set<BusinessPolicyVersion>();
    public DbSet<BusinessPolicyActivation> BusinessPolicyActivations => Set<BusinessPolicyActivation>();
    public DbSet<BusinessPolicyActivationDecision> BusinessPolicyActivationDecisions => Set<BusinessPolicyActivationDecision>();
    public DbSet<SurveyRequestQuoteSnapshot> SurveyRequestQuoteSnapshots => Set<SurveyRequestQuoteSnapshot>();
    public DbSet<SurveyRequestTracking> SurveyRequestTrackings => Set<SurveyRequestTracking>();
    public DbSet<SurveyOrderCommercialSnapshot> SurveyOrderCommercialSnapshots => Set<SurveyOrderCommercialSnapshot>();
    public DbSet<OrderStateHistory> OrderStateHistories => Set<OrderStateHistory>();
    public DbSet<CaptureAttempt> CaptureAttempts => Set<CaptureAttempt>();
    public DbSet<CaptureAttemptHistory> CaptureAttemptHistories => Set<CaptureAttemptHistory>();
    public DbSet<CaptureInputManifest> CaptureInputManifests => Set<CaptureInputManifest>();
    public DbSet<CaptureInputManifestFile> CaptureInputManifestFiles => Set<CaptureInputManifestFile>();
    public DbSet<CaptureInputApproval> CaptureInputApprovals => Set<CaptureInputApproval>();
    public DbSet<SpatialPolicy> SpatialPolicies => Set<SpatialPolicy>();
    public DbSet<SurveyOrderPoleCountSnapshot> SurveyOrderPoleCountSnapshots => Set<SurveyOrderPoleCountSnapshot>();
    public DbSet<SurveyOrderPoleCountSnapshotItem> SurveyOrderPoleCountSnapshotItems => Set<SurveyOrderPoleCountSnapshotItem>();
    public DbSet<SurveyDepositObligation> SurveyDepositObligations => Set<SurveyDepositObligation>();
    public DbSet<SurveyFundingReceipt> SurveyFundingReceipts => Set<SurveyFundingReceipt>();
    public DbSet<SurveyFundingAllocation> SurveyFundingAllocations => Set<SurveyFundingAllocation>();
    public DbSet<SurveyFundingReconciliationDecision> SurveyFundingReconciliationDecisions => Set<SurveyFundingReconciliationDecision>();
    public DbSet<SurveyCancellationDecision> SurveyCancellationDecisions => Set<SurveyCancellationDecision>();
    public DbSet<SurveyRefundObligation> SurveyRefundObligations => Set<SurveyRefundObligation>();
    public DbSet<CancellationRetention> CancellationRetentions => Set<CancellationRetention>();
    public DbSet<SurveyInternalApprovedResultRevision> SurveyInternalApprovedResultRevisions => Set<SurveyInternalApprovedResultRevision>();
    public DbSet<SurveyInvoiceRevision> SurveyInvoiceRevisions => Set<SurveyInvoiceRevision>();
    public DbSet<SurveyComboImportBatch> SurveyComboImportBatches => Set<SurveyComboImportBatch>();
    public DbSet<SurveyComboReviewRevision> SurveyComboReviewRevisions => Set<SurveyComboReviewRevision>();
    public DbSet<SurveyResultReviewOperation> SurveyResultReviewOperations => Set<SurveyResultReviewOperation>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public void AddAuditLog(AuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        AuditLogs.Add(auditLog);
    }

    public async Task AcquireBusinessPolicyWriteLockAsync(
        CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return;
        }

        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The business-policy write guard requires an active transaction.");
        }

        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({BusinessPolicyLockKeys.ActivationGuard})",
            cancellationToken);
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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<System.Text.Json.JsonDocument>()
            .HaveConversion<JsonDocumentStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("survey");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveysDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }

    private sealed class JsonDocumentStringConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonDocument, string>
    {
        public JsonDocumentStringConverter()
            : base(
                doc => doc == null ? "{}" : doc.RootElement.GetRawText(),
                text => System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text, default))
        {
        }
    }
}
