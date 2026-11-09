using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface IQualityRefundWorkflow
{
    Task<Result<QualityRefundCaseView>> ReportAsync(
        Guid orderId,
        IReadOnlyList<ReportedPoleIssue> issues,
        string description,
        string? evidenceReference,
        string recipientBankName,
        string recipientAccountNumber,
        string recipientAccountName,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<QualityRefundCaseView>>> GetOwnerCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ManagerQualityRefundCaseView>>> GetManagerCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Result<ManagerQualityRefundCaseView>> VerifyByManagerAsync(
        Guid orderId,
        Guid caseId,
        IReadOnlyList<VerifiedPoleIssue> findings,
        string reason,
        uint expectedVersion,
        CancellationToken cancellationToken);

    Task<Result<QualityRefundOrderView>> GetAdminCasesAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<QualityRefundQueueItem>>> GetAdminQueueAsync(
        QualityRefundStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<QualityRefundCaseView>> DecideAsync(
        Guid orderId,
        Guid caseId,
        bool approve,
        string reason,
        uint expectedVersion,
        CancellationToken cancellationToken);

    Task<Result<QualityRefundCaseView>> RecordTransferAsync(
        Guid orderId,
        Guid caseId,
        string bankTransferReference,
        uint expectedVersion,
        CancellationToken cancellationToken);
}

public sealed record QualityRefundCaseView(
    Guid Id,
    Guid SurveyOrderId,
    Guid SurveyResultId,
    QualityRefundStatus Status,
    IReadOnlyList<ReportedPoleIssue> Issues,
    string Description,
    string? EvidenceReference,
    string RecipientBankName,
    string RecipientAccountNumber,
    string RecipientAccountName,
    IReadOnlyList<VerifiedPoleIssue>? ManagerFindings,
    Guid? ManagerVerifiedBy,
    DateTimeOffset? ManagerVerifiedAt,
    string? ManagerVerificationReason,
    string? DecisionReason,
    decimal? ApprovedAmount,
    string? BankTransferReference,
    string? TransferEvidenceReference,
    DateTimeOffset? TransferredAt,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ManagerQualityRefundCaseView(
    Guid Id,
    Guid SurveyOrderId,
    Guid SurveyResultId,
    QualityRefundStatus Status,
    IReadOnlyList<ReportedPoleIssue> Issues,
    string Description,
    string? EvidenceReference,
    IReadOnlyList<VerifiedPoleIssue>? ManagerFindings,
    Guid? ManagerVerifiedBy,
    DateTimeOffset? ManagerVerifiedAt,
    string? ManagerVerificationReason,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record QualityRefundOrderView(
    Guid SurveyOrderId,
    decimal GrossPaid,
    decimal Refunded,
    decimal NetCollected,
    string Currency,
    IReadOnlyList<QualityRefundCaseView> Cases);

public sealed record QualityRefundQueueItem(
    string OrderNumber,
    QualityRefundCaseView RefundCase);
