namespace AgriDrone.IntegrationContracts.Surveys;

public enum SurveySettlementReleaseOutcome
{
    Released = 1,
    Replay = 2,
    InsufficientFunding = 3,
    StaleContext = 4,
    ReconciliationHold = 5,
    Forbidden = 6,
    NotFound = 7,
    Blocked = 8
}

public enum SurveyReleaseTriggerSource
{
    InvoiceFinalization = 1,
    PreReleaseCorrection = 2,
    PaymentCallback = 3,
    AdminReconciliation = 4,
    RetryEvaluation = 5
}

/// <summary>
/// Request aligned with the V3-05F handoff token contract to evaluate settlement
/// and execute atomic release in an isolated Rank-1-ordered transaction.
/// </summary>
public sealed record EvaluateSurveySettlementReleaseRequest(
    Guid OrderId,
    SurveyReleaseTriggerSource TriggerSource,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint,
    Guid? TriggerActorId = null,
    Guid? ExpectedTenantId = null,
    uint? ExpectedOrderVersion = null,
    Guid? ExpectedApprovedResultRevisionId = null,
    string? ExpectedApprovedResultFingerprint = null,
    Guid? ExpectedCountSnapshotId = null,
    string? ExpectedCountSnapshotFingerprint = null,
    Guid? ExpectedInvoiceRevisionId = null,
    int? ExpectedInvoiceRevisionNumber = null,
    string? ExpectedInvoiceFingerprint = null,
    Guid? ExpectedCommercialPolicyVersionId = null,
    Guid? ExpectedSurveyServicePriceId = null,
    decimal? ExpectedPricePerPole = null,
    string? ExpectedCurrency = null,
    Guid? TriggerPaymentId = null,
    Guid? TriggerFundingReceiptId = null,
    Guid? ExpectedFarmId = null);

public sealed record EvaluateSurveySettlementReleaseResult(
    SurveySettlementReleaseOutcome Outcome,
    Guid OrderId,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    uint? OrderVersion = null,
    string? OrderStatus = null,
    Guid? ApprovedResultRevisionId = null,
    string? ApprovedResultFingerprint = null,
    Guid? CountSnapshotId = null,
    string? CountSnapshotFingerprint = null,
    Guid? InvoiceRevisionId = null,
    int? InvoiceRevisionNumber = null,
    string? InvoiceFingerprint = null,
    decimal? TotalAmount = null,
    decimal? ValidAllocatedDepositNet = null,
    decimal? ConfirmedNetReceipts = null,
    decimal? DueAmount = null,
    decimal? OverpaymentAmount = null,
    Guid? ReservedOverpaymentObligationId = null,
    string? Currency = null,
    Guid? ReleaseReceiptId = null,
    V3ResultReleaseProof? ReleaseProof = null)
{
    public bool Succeeded =>
        Outcome is SurveySettlementReleaseOutcome.Released or SurveySettlementReleaseOutcome.Replay;

    public bool IsReleased => Outcome == SurveySettlementReleaseOutcome.Released;

    public bool IsReplay => Outcome == SurveySettlementReleaseOutcome.Replay;

    public static EvaluateSurveySettlementReleaseResult Released(
        Guid orderId,
        uint orderVersion,
        string orderStatus,
        Guid approvedResultRevisionId,
        string approvedResultFingerprint,
        Guid countSnapshotId,
        string countSnapshotFingerprint,
        Guid invoiceRevisionId,
        int invoiceRevisionNumber,
        string invoiceFingerprint,
        decimal totalAmount,
        decimal validAllocatedDepositNet,
        decimal confirmedNetReceipts,
        decimal overpaymentAmount,
        Guid? reservedOverpaymentObligationId,
        string currency,
        Guid releaseReceiptId,
        V3ResultReleaseProof releaseProof) => new(
            Outcome: SurveySettlementReleaseOutcome.Released,
            OrderId: orderId,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus,
            ApprovedResultRevisionId: approvedResultRevisionId,
            ApprovedResultFingerprint: approvedResultFingerprint,
            CountSnapshotId: countSnapshotId,
            CountSnapshotFingerprint: countSnapshotFingerprint,
            InvoiceRevisionId: invoiceRevisionId,
            InvoiceRevisionNumber: invoiceRevisionNumber,
            InvoiceFingerprint: invoiceFingerprint,
            TotalAmount: totalAmount,
            ValidAllocatedDepositNet: validAllocatedDepositNet,
            ConfirmedNetReceipts: confirmedNetReceipts,
            DueAmount: 0m,
            OverpaymentAmount: overpaymentAmount,
            ReservedOverpaymentObligationId: reservedOverpaymentObligationId,
            Currency: currency,
            ReleaseReceiptId: releaseReceiptId,
            ReleaseProof: releaseProof);

    public static EvaluateSurveySettlementReleaseResult Replay(
        Guid orderId,
        uint orderVersion,
        string orderStatus,
        Guid approvedResultRevisionId,
        string approvedResultFingerprint,
        Guid countSnapshotId,
        string countSnapshotFingerprint,
        Guid invoiceRevisionId,
        int invoiceRevisionNumber,
        string invoiceFingerprint,
        decimal totalAmount,
        decimal validAllocatedDepositNet,
        decimal confirmedNetReceipts,
        decimal overpaymentAmount,
        Guid? reservedOverpaymentObligationId,
        string currency,
        Guid releaseReceiptId,
        V3ResultReleaseProof releaseProof) => new(
            Outcome: SurveySettlementReleaseOutcome.Replay,
            OrderId: orderId,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus,
            ApprovedResultRevisionId: approvedResultRevisionId,
            ApprovedResultFingerprint: approvedResultFingerprint,
            CountSnapshotId: countSnapshotId,
            CountSnapshotFingerprint: countSnapshotFingerprint,
            InvoiceRevisionId: invoiceRevisionId,
            InvoiceRevisionNumber: invoiceRevisionNumber,
            InvoiceFingerprint: invoiceFingerprint,
            TotalAmount: totalAmount,
            ValidAllocatedDepositNet: validAllocatedDepositNet,
            ConfirmedNetReceipts: confirmedNetReceipts,
            DueAmount: 0m,
            OverpaymentAmount: overpaymentAmount,
            ReservedOverpaymentObligationId: reservedOverpaymentObligationId,
            Currency: currency,
            ReleaseReceiptId: releaseReceiptId,
            ReleaseProof: releaseProof);

    public static EvaluateSurveySettlementReleaseResult InsufficientFunding(
        Guid orderId,
        uint orderVersion,
        string orderStatus,
        Guid approvedResultRevisionId,
        string approvedResultFingerprint,
        Guid countSnapshotId,
        string countSnapshotFingerprint,
        Guid invoiceRevisionId,
        int invoiceRevisionNumber,
        string invoiceFingerprint,
        decimal totalAmount,
        decimal validAllocatedDepositNet,
        decimal confirmedNetReceipts,
        decimal dueAmount,
        string currency,
        string errorCode = "SettlementRelease.InsufficientFunding",
        string errorMessage = "Confirmed settled funding is less than the finalized invoice total.") => new(
            Outcome: SurveySettlementReleaseOutcome.InsufficientFunding,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus,
            ApprovedResultRevisionId: approvedResultRevisionId,
            ApprovedResultFingerprint: approvedResultFingerprint,
            CountSnapshotId: countSnapshotId,
            CountSnapshotFingerprint: countSnapshotFingerprint,
            InvoiceRevisionId: invoiceRevisionId,
            InvoiceRevisionNumber: invoiceRevisionNumber,
            InvoiceFingerprint: invoiceFingerprint,
            TotalAmount: totalAmount,
            ValidAllocatedDepositNet: validAllocatedDepositNet,
            ConfirmedNetReceipts: confirmedNetReceipts,
            DueAmount: dueAmount,
            OverpaymentAmount: 0m,
            ReservedOverpaymentObligationId: null,
            Currency: currency);

    public static EvaluateSurveySettlementReleaseResult StaleContext(
        Guid orderId,
        string errorCode,
        string errorMessage,
        uint? orderVersion = null,
        string? orderStatus = null,
        Guid? invoiceRevisionId = null,
        int? invoiceRevisionNumber = null,
        string? invoiceFingerprint = null) => new(
            Outcome: SurveySettlementReleaseOutcome.StaleContext,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus,
            InvoiceRevisionId: invoiceRevisionId,
            InvoiceRevisionNumber: invoiceRevisionNumber,
            InvoiceFingerprint: invoiceFingerprint);

    public static EvaluateSurveySettlementReleaseResult ReconciliationHold(
        Guid orderId,
        string errorCode,
        string errorMessage,
        uint? orderVersion = null,
        string? orderStatus = null,
        Guid? invoiceRevisionId = null) => new(
            Outcome: SurveySettlementReleaseOutcome.ReconciliationHold,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus,
            InvoiceRevisionId: invoiceRevisionId);

    public static EvaluateSurveySettlementReleaseResult Forbidden(
        Guid orderId,
        string errorCode = "SettlementRelease.Forbidden",
        string errorMessage = "Caller or approval assignment is not authorized for this farm.") => new(
            Outcome: SurveySettlementReleaseOutcome.Forbidden,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);

    public static EvaluateSurveySettlementReleaseResult NotFound(
        Guid orderId,
        string errorCode = "Order.NotFound",
        string errorMessage = "Survey order was not found.") => new(
            Outcome: SurveySettlementReleaseOutcome.NotFound,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);

    public static EvaluateSurveySettlementReleaseResult Blocked(
        Guid orderId,
        string errorCode,
        string errorMessage,
        uint? orderVersion = null,
        string? orderStatus = null) => new(
            Outcome: SurveySettlementReleaseOutcome.Blocked,
            OrderId: orderId,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            OrderVersion: orderVersion,
            OrderStatus: orderStatus);
}

/// <summary>
/// Opens its own isolated ADR-0020 Rank-1-ordered transaction to authorize before reading receipts,
/// recheck exact V3-05F tokens and funding facts, and commit atomic release artifacts.
/// </summary>
public interface ISurveySettlementReleaseCoordinator
{
    Task<EvaluateSurveySettlementReleaseResult> EvaluateAndReleaseAsync(
        EvaluateSurveySettlementReleaseRequest request,
        CancellationToken cancellationToken = default);
}
