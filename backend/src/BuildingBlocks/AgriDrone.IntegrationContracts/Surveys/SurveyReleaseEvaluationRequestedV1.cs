namespace AgriDrone.IntegrationContracts.Surveys;

/// <summary>
/// Requests release evaluation in a separate Rank-1-ordered transaction with bounded retry.
/// The producer writes the trigger in the same commit as the invoice or funding change.
/// </summary>
public sealed record SurveyReleaseEvaluationRequestedV1(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    string TriggerSource,
    Guid OperationId,
    string PayloadFingerprint,
    DateTimeOffset RequestedAt,
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
    Guid? TriggerActorId = null,
    Guid? TriggerPaymentId = null,
    Guid? TriggerFundingReceiptId = null);
