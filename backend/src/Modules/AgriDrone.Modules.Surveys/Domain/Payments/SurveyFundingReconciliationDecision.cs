using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyReconciliationDispositionKind
{
    KeepExcluded = 1,
    AllocateToCurrentInvoice = 2,
    RouteToRefund = 3
}

public sealed partial class SurveyFundingReconciliationDecision : Entity
{
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.Compiled)]
    private static partial Regex Hex64Regex();

    private SurveyFundingReconciliationDecision() { }

    public Guid TenantId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public SurveyReconciliationDispositionKind DispositionKind { get; private set; }
    public Guid? SurveyPaymentId { get; private set; }
    public Guid? FundingReceiptId { get; private set; }
    public Guid? TargetInvoiceRevisionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string? ProviderReference { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string Reason { get; private set; } = null!;
    public string EvidenceReference { get; private set; } = null!;
    public uint ExpectedOrderVersion { get; private set; }
    public Guid ReconciledBy { get; private set; }
    public DateTimeOffset ReconciledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyFundingReconciliationDecision Create(
        Guid id,
        Guid tenantId,
        Guid surveyOrderId,
        Guid operationId,
        string payloadFingerprint,
        SurveyReconciliationDispositionKind dispositionKind,
        Guid? surveyPaymentId,
        Guid? fundingReceiptId,
        Guid? targetInvoiceRevisionId,
        decimal amount,
        string currency,
        string? providerReference,
        string? providerTransactionId,
        string reason,
        string evidenceReference,
        uint expectedOrderVersion,
        Guid reconciledBy,
        DateTimeOffset reconciledAt,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(operationId, nameof(operationId));
        DomainGuard.NotEmpty(reconciledBy, nameof(reconciledBy));
        DomainGuard.Utc(reconciledAt, nameof(reconciledAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (!Enum.IsDefined(dispositionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(dispositionKind), "Unsupported reconciliation disposition.");
        }

        if (createdAt < reconciledAt)
        {
            throw new ArgumentException("CreatedAt cannot be earlier than ReconciledAt.", nameof(createdAt));
        }

        if (expectedOrderVersion == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedOrderVersion), "ExpectedOrderVersion must be at least 1.");
        }

        if (!surveyPaymentId.HasValue && !fundingReceiptId.HasValue)
        {
            throw new ArgumentException("At least one of SurveyPaymentId or FundingReceiptId is required.", nameof(surveyPaymentId));
        }

        if (surveyPaymentId.HasValue)
        {
            DomainGuard.NotEmpty(surveyPaymentId.Value, nameof(surveyPaymentId));
        }

        if (fundingReceiptId.HasValue)
        {
            DomainGuard.NotEmpty(fundingReceiptId.Value, nameof(fundingReceiptId));
        }

        if (targetInvoiceRevisionId.HasValue)
        {
            DomainGuard.NotEmpty(targetInvoiceRevisionId.Value, nameof(targetInvoiceRevisionId));
        }

        if (dispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice &&
            !targetInvoiceRevisionId.HasValue)
        {
            throw new ArgumentException(
                "TargetInvoiceRevisionId is required when allocating reconciliation funds to an invoice.",
                nameof(targetInvoiceRevisionId));
        }

        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(
            payloadFingerprint, 64, nameof(payloadFingerprint)).ToLowerInvariant();
        if (!Hex64Regex().IsMatch(normalizedFingerprint))
        {
            throw new ArgumentException("PayloadFingerprint must be a 64-character hex SHA-256 digest.", nameof(payloadFingerprint));
        }

        var validatedAmount = dispositionKind == SurveyReconciliationDispositionKind.KeepExcluded && amount == 0m
            ? 0m
            : PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 2000, nameof(reason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeRequired(evidenceReference, 1000, nameof(evidenceReference));
        var normalizedRef = PolicyValidationGuard.NormalizeOptional(providerReference, 150, nameof(providerReference));
        var normalizedTxId = PolicyValidationGuard.NormalizeOptional(providerTransactionId, 150, nameof(providerTransactionId));

        return new SurveyFundingReconciliationDecision
        {
            Id = id,
            TenantId = tenantId,
            SurveyOrderId = surveyOrderId,
            OperationId = operationId,
            PayloadFingerprint = normalizedFingerprint,
            DispositionKind = dispositionKind,
            SurveyPaymentId = surveyPaymentId,
            FundingReceiptId = fundingReceiptId,
            TargetInvoiceRevisionId = targetInvoiceRevisionId,
            Amount = validatedAmount,
            Currency = validatedCurrency,
            ProviderReference = normalizedRef,
            ProviderTransactionId = normalizedTxId,
            Reason = normalizedReason,
            EvidenceReference = normalizedEvidence,
            ExpectedOrderVersion = expectedOrderVersion,
            ReconciledBy = reconciledBy,
            ReconciledAt = reconciledAt,
            CreatedAt = createdAt
        };
    }
}
