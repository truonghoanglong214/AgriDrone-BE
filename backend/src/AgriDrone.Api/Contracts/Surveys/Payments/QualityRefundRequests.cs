namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ReportedPoleIssueRequest(
    string PoleCode,
    string IssueType,
    string Details);

public sealed record ReportQualityRefundRequest(
    IReadOnlyList<ReportedPoleIssueRequest> Issues,
    string Description,
    string? EvidenceReference,
    string RecipientBankName,
    string RecipientAccountNumber,
    string RecipientAccountName);

public sealed record DecideQualityRefundRequest(
    bool Approve,
    string Reason,
    uint ExpectedVersion);

public sealed record VerifiedPoleIssueRequest(
    string PoleCode,
    bool ConfirmedError,
    string Reason);

public sealed record VerifyQualityRefundRequest(
    IReadOnlyList<VerifiedPoleIssueRequest> Findings,
    string Reason,
    uint ExpectedVersion);

public sealed record RecordQualityRefundTransferRequest(
    string BankTransferReference,
    uint ExpectedVersion);
