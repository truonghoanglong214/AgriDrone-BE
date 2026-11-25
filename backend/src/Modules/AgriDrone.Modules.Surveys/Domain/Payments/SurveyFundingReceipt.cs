using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyFundingReceipt : Entity
{
    private SurveyFundingReceipt() { }

    public Guid TenantId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid? DepositObligationId { get; private set; }
    public Guid? SurveyPaymentId { get; private set; }
    public Guid? InvoiceRevisionId { get; private set; }
    public bool RequiresReconciliation { get; private set; }
    public string? ReconciliationReason { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ProviderTransactionId { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string PaymentReference { get; private set; } = null!;
    public string PayloadFingerprint { get; private set; } = null!;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyFundingReceipt Create(
        Guid id,
        Guid tenantId,
        Guid surveyOrderId,
        Guid? depositObligationId,
        string provider,
        string providerTransactionId,
        decimal amount,
        string currency,
        string paymentReference,
        string payloadFingerprint,
        DateTimeOffset receivedAt,
        DateTimeOffset createdAt,
        Guid? surveyPaymentId = null,
        Guid? invoiceRevisionId = null,
        bool requiresReconciliation = false,
        string? reconciliationReason = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        if (depositObligationId.HasValue)
        {
            DomainGuard.NotEmpty(depositObligationId.Value, nameof(depositObligationId));
        }
        if (surveyPaymentId.HasValue)
        {
            DomainGuard.NotEmpty(surveyPaymentId.Value, nameof(surveyPaymentId));
        }
        if (invoiceRevisionId.HasValue)
        {
            DomainGuard.NotEmpty(invoiceRevisionId.Value, nameof(invoiceRevisionId));
            if (!surveyPaymentId.HasValue)
            {
                throw new ArgumentException("InvoiceRevisionId requires a linked SurveyPaymentId.", nameof(invoiceRevisionId));
            }
        }
        DomainGuard.Utc(receivedAt, nameof(receivedAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        var normalizedProvider = PolicyValidationGuard.NormalizeRequired(provider, 64, nameof(provider));
        var normalizedTxId = PolicyValidationGuard.NormalizeRequired(providerTransactionId, 150, nameof(providerTransactionId));
        var normalizedRef = PolicyValidationGuard.NormalizeRequired(paymentReference, 150, nameof(paymentReference));
        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(payloadFingerprint, 128, nameof(payloadFingerprint));
        var normalizedReconciliationReason = PolicyValidationGuard.NormalizeOptional(reconciliationReason, 500, nameof(reconciliationReason));

        if (requiresReconciliation && normalizedReconciliationReason is null)
        {
            throw new ArgumentException("ReconciliationReason is required when RequiresReconciliation is true.", nameof(reconciliationReason));
        }

        if (!requiresReconciliation && normalizedReconciliationReason is not null)
        {
            throw new ArgumentException("ReconciliationReason must be null when RequiresReconciliation is false.", nameof(reconciliationReason));
        }

        return new SurveyFundingReceipt
        {
            Id = id,
            TenantId = tenantId,
            SurveyOrderId = surveyOrderId,
            DepositObligationId = depositObligationId,
            SurveyPaymentId = surveyPaymentId,
            InvoiceRevisionId = invoiceRevisionId,
            RequiresReconciliation = requiresReconciliation,
            ReconciliationReason = normalizedReconciliationReason,
            Provider = normalizedProvider,
            ProviderTransactionId = normalizedTxId,
            Amount = validatedAmount,
            Currency = validatedCurrency,
            PaymentReference = normalizedRef,
            PayloadFingerprint = normalizedFingerprint,
            ReceivedAt = receivedAt,
            CreatedAt = createdAt
        };
    }
}
