using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public enum V3BalanceCheckoutOutcome
{
    Created = 1,
    Replay = 2,
    InvalidatedByCorrection = 3,
    InsufficientFunding = 4,
    ZeroBalanceSettled = 5,
    StaleContext = 6,
    ReconciliationHold = 7,
    ActiveCheckoutExists = 8,
    ProviderUnavailable = 9,
    ProviderUncertain = 10,
    Forbidden = 11,
    NotFound = 12
}

public sealed record V3BalanceCheckoutIntentRequest(
    Guid OrderId,
    Guid TenantId,
    Guid ActorId,
    Guid OperationId,
    string PayloadFingerprint,
    uint ExpectedOrderVersion,
    Guid ExpectedInvoiceRevisionId,
    string ExpectedInvoiceFingerprint,
    Guid ExpectedApprovedResultRevisionId,
    string ExpectedApprovedResultFingerprint,
    Guid ExpectedCountSnapshotId,
    string ExpectedCountSnapshotFingerprint,
    string Provider,
    Guid CorrelationId,
    Guid? ExpectedFarmId = null);

public sealed record V3BalanceCheckoutIntentResult(
    V3BalanceCheckoutOutcome Outcome,
    Guid OrderId,
    Guid? PaymentId = null,
    string? OrderNumber = null,
    uint? OrderVersion = null,
    Guid? InvoiceRevisionId = null,
    int? InvoiceRevisionNumber = null,
    string? InvoiceFingerprint = null,
    decimal? DueAmount = null,
    string? Currency = null,
    string? Provider = null,
    string? ExistingProviderReference = null,
    DateTimeOffset? IntentCreatedAt = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    V3BalanceCheckoutResult? ExistingOutcome = null);

public sealed record V3BalanceCheckoutOutcomeRequest(
    Guid OrderId,
    Guid PaymentId,
    Guid ActorId,
    Guid TenantId,
    Guid OperationId,
    Guid ExpectedInvoiceRevisionId,
    string ExpectedInvoiceFingerprint,
    bool ProviderSucceeded,
    bool ProviderOutcomeUncertain,
    string? ProviderReference,
    Uri? RedirectUrl,
    string? ClientToken,
    DateTimeOffset? ExpiresAt,
    string? FailureCode,
    string? FailureMessage,
    Guid CorrelationId);

public sealed record V3BalanceCheckoutResult(
    V3BalanceCheckoutOutcome Outcome,
    Guid OrderId,
    Guid? PaymentId = null,
    uint? PaymentVersion = null,
    uint? OrderVersion = null,
    Guid? InvoiceRevisionId = null,
    int? InvoiceRevisionNumber = null,
    string? InvoiceFingerprint = null,
    decimal? Amount = null,
    string? Currency = null,
    string? Provider = null,
    string? ProviderReference = null,
    string? PaymentStatus = null,
    Uri? RedirectUrl = null,
    string? ClientToken = null,
    DateTimeOffset? ExpiresAt = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Enforces the A2.1 V3 FinalBalance checkout transaction split:
/// (1) Persist Pending checkout intent bound to the exact invoice revision under Rank 1->2->5->6 locks and commit;
/// (2) Call SePay outside any SQL transaction;
/// (3) Re-acquire locks in a new transaction, recheck invoice revision/status, commit outcome or preserve reference on invalidated checkouts.
/// </summary>
public interface IV3BalanceCheckoutTransaction
{
    Task<Result<V3BalanceCheckoutIntentResult>> BeginCheckoutIntentAsync(
        V3BalanceCheckoutIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<V3BalanceCheckoutResult>> CompleteCheckoutOutcomeAsync(
        V3BalanceCheckoutOutcomeRequest request,
        CancellationToken cancellationToken = default);
}
