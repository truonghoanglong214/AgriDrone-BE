namespace AgriDrone.IntegrationContracts.Surveys;

/// <summary>
/// Authoritative Plan 2 release proof handed off from V3-06 to V3-07/V3-08/V3-09.
/// Catalogue flags, Completed order statuses, and legacy publications are never release evidence.
/// </summary>
public sealed record V3ResultReleaseProof(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    Guid PublicationId,
    int PublicationVersion,
    Guid? PredecessorPublicationId,
    bool IsReplacement,
    bool IsSubstantiveReplacement,
    bool IsWithdrawn,
    DateTimeOffset? WithdrawnAt,
    Guid ApprovedResultRevisionId,
    string ApprovedResultFingerprint,
    Guid InvoiceRevisionId,
    string InvoiceFingerprint,
    string ServiceCode,
    bool ScreensDisease,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid? ComboReviewRevisionId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset PaidAt,
    DateTimeOffset PublishedAt,
    DateTimeOffset UnlockedAt,
    DateTimeOffset NoticeCreatedAt,
    Guid ClaimWindowId,
    DateTimeOffset ClaimDeadlineExclusiveUtc,
    Guid ClaimPolicyVersionId)
{
    public bool AllowsOwnerActiveAccess(Guid tenantId, Guid farmId) =>
        tenantId != Guid.Empty &&
        farmId != Guid.Empty &&
        TenantId == tenantId &&
        FarmId == farmId &&
        OrderId != Guid.Empty &&
        PublicationId != Guid.Empty &&
        !IsWithdrawn;
}

/// <summary>
/// Tenant- and authorization-aware Plan 2 port for reading durable V3 release proof.
/// Returns null when the order/publication has no committed release proof or is unavailable.
/// </summary>
public interface IV3ResultReleaseProofReader
{
    Task<V3ResultReleaseProof?> GetByOrderIdAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<V3ResultReleaseProof?> GetByPublicationIdAsync(
        Guid tenantId,
        Guid publicationId,
        CancellationToken cancellationToken = default);
}

public static class V3LedgerRefundOriginKinds
{
    public const string Overpayment = "Overpayment";
    public const string CancellationReturn = "CancellationReturn";
    public const string ServiceFailure = "ServiceFailure";
    public const string ComplaintRefund = "ComplaintRefund";
}

/// <summary>
/// Immutable original service recognition recognized once per V3 Order at release time.
/// </summary>
public sealed record V3OriginalServiceRecognitionFact(
    Guid RecognitionId,
    Guid OrderId,
    Guid PublicationId,
    Guid InvoiceRevisionId,
    string ServiceCode,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset RecognizedAt);

public sealed record V3LedgerFundingReceiptFact(
    Guid ReceiptId,
    Guid? PaymentId,
    Guid? DepositObligationId,
    Guid? InvoiceRevisionId,
    string Provider,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    bool RequiresReconciliation,
    string? ReconciliationReason,
    DateTimeOffset ReceivedAt);

public sealed record V3LedgerFundingAllocationFact(
    Guid AllocationId,
    Guid FundingReceiptId,
    Guid DepositObligationId,
    decimal AllocatedAmount,
    string Currency,
    DateTimeOffset AllocatedAt);

public sealed record V3LedgerFundingReversalFact(
    Guid ReversalId,
    Guid PaymentId,
    Guid? FundingReceiptId,
    Guid? DepositObligationId,
    decimal? CashAmount,
    decimal AllocatedDeltaAmount,
    string Currency,
    bool RequiresReconciliation,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt);

public sealed record V3LedgerRefundObligationFact(
    Guid RefundObligationId,
    string OriginKind,
    Guid OriginDecisionId,
    Guid? FundingReceiptId,
    decimal Amount,
    string Currency,
    string Status,
    bool ReturnsServiceValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReservedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt);

/// <summary>
/// Plan 2 V3-09 reporting handshake preserving immutable lineage between service value recognition,
/// confirmed receipts, deposit allocations, cash/allocation reversals, and refund obligations.
/// </summary>
public sealed record V3OrderLedgerFacts(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    string ServiceCode,
    string Currency,
    decimal ConfirmedNetReceipts,
    decimal ValidAllocatedDepositNet,
    bool HasReconciliationHold,
    V3OriginalServiceRecognitionFact? OriginalServiceRecognition,
    IReadOnlyList<V3LedgerFundingReceiptFact> FundingReceipts,
    IReadOnlyList<V3LedgerFundingAllocationFact> FundingAllocations,
    IReadOnlyList<V3LedgerFundingReversalFact> FundingReversals,
    IReadOnlyList<V3LedgerRefundObligationFact> RefundObligations);

/// <summary>
/// Read port exposing V3 ledger facts for reporting and reconciliation without double-counting.
/// </summary>
public interface IV3FinancialLedgerFactsReader
{
    Task<V3OrderLedgerFacts?> GetOrderLedgerFactsAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}
