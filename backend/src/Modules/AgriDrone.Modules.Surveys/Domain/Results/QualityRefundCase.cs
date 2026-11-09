using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum QualityRefundStatus
{
    Submitted,
    Rejected,
    Approved,
    Refunded
}

public sealed record ReportedPoleIssue(string PoleCode, string IssueType, string Details);
public sealed record VerifiedPoleIssue(string PoleCode, bool ConfirmedError, string Reason);

public sealed class QualityRefundCase : Entity
{
    private QualityRefundCase() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid ReportedBy { get; private set; }
    public JsonDocument ReportedIssues { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }
    public string RecipientBankName { get; private set; } = null!;
    public string RecipientAccountNumber { get; private set; } = null!;
    public string RecipientAccountName { get; private set; } = null!;
    public QualityRefundStatus Status { get; private set; }
    public JsonDocument? ManagerFindings { get; private set; }
    public Guid? ManagerVerifiedBy { get; private set; }
    public DateTimeOffset? ManagerVerifiedAt { get; private set; }
    public string? ManagerVerificationReason { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string? BankTransferReference { get; private set; }
    public string? TransferEvidenceReference { get; private set; }
    public DateTimeOffset? TransferredAt { get; private set; }
    public Guid? RecordedBy { get; private set; }
    public DateTimeOffset? RecordedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;
    public SurveyResult SurveyResult { get; private set; } = null!;

    public static QualityRefundCase Report(
        SurveyOrder order,
        SurveyResult result,
        Guid reportedBy,
        IReadOnlyList<ReportedPoleIssue> issues,
        string description,
        string? evidenceReference,
        string recipientBankName,
        string recipientAccountNumber,
        string recipientAccountName,
        DateTimeOffset now)
    {
        DomainGuard.NotEmpty(reportedBy);
        DomainGuard.Utc(now);
        if (order.Status != SurveyOrderStatus.Completed ||
            result.Status != SurveyResultStatus.Published ||
            result.InvalidatedAt.HasValue ||
            result.SurveyOrderId != order.Id ||
            result.TenantId != order.TenantId ||
            result.FarmId != order.FarmId ||
            result.ServiceType != SurveyServiceType.PlantHealth)
        {
            throw new InvalidOperationException("Only a current published Plant Health result can be reported.");
        }

        if (issues is null || issues.Count < 1 ||
            issues.Any(item =>
                item is null || string.IsNullOrWhiteSpace(item.PoleCode) || item.PoleCode.Length > 100 ||
                item.IssueType is not ("FALSE_POSITIVE" or "FALSE_NEGATIVE" or "WRONG_POLE") ||
                string.IsNullOrWhiteSpace(item.Details) || item.Details.Length > 1000) ||
            issues.Select(item => item.PoleCode.Trim().ToUpperInvariant()).Distinct().Count() != issues.Count)
        {
            throw new ArgumentException("Provide at least one distinct pole issue with a valid type and description.", nameof(issues));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 2000 || evidenceReference?.Length > 1000)
        {
            throw new ArgumentException("Report text exceeds the allowed length.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientBankName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientAccountNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientAccountName);
        if (recipientBankName.Trim().Length > 150 ||
            recipientAccountNumber.Trim().Length is < 4 or > 34 ||
            recipientAccountName.Trim().Length > 150 ||
            !recipientAccountNumber.Trim().All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Provide a valid bank recipient name and numeric account number.");
        }

        return new QualityRefundCase
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = order.Id,
            TenantId = order.TenantId,
            FarmId = order.FarmId,
            SurveyResultId = result.Id,
            ReportedBy = reportedBy,
            ReportedIssues = JsonSerializer.SerializeToDocument(issues.Select(item =>
                new ReportedPoleIssue(item.PoleCode.Trim(), item.IssueType, item.Details.Trim()))),
            Description = description.Trim(),
            EvidenceReference = evidenceReference?.Trim(),
            RecipientBankName = recipientBankName.Trim(),
            RecipientAccountNumber = recipientAccountNumber.Trim(),
            RecipientAccountName = recipientAccountName.Trim(),
            Status = QualityRefundStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void VerifyByManager(
        Guid managerId,
        IReadOnlyList<VerifiedPoleIssue> findings,
        string reason,
        DateTimeOffset now,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.NotEmpty(managerId);
        DomainGuard.Utc(now);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != QualityRefundStatus.Submitted || ManagerVerifiedAt.HasValue ||
            reason.Trim().Length > 2000 || now < UpdatedAt || findings is null)
        {
            throw new InvalidOperationException("Only an unverified submitted report can be verified.");
        }

        var reported = ReportedIssues.RootElement.Deserialize<ReportedPoleIssue[]>() ?? [];
        var reportedCodes = reported.Select(item => item.PoleCode.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findingCodes = findings.Select(item => item?.PoleCode?.Trim() ?? string.Empty).ToArray();
        if (findings.Count != reported.Length ||
            findingCodes.Any(string.IsNullOrWhiteSpace) ||
            findingCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != findings.Count ||
            findingCodes.Any(code => !reportedCodes.Contains(code)) ||
            findings.Any(item => item is null || string.IsNullOrWhiteSpace(item.Reason) ||
                item.Reason.Length > 1000))
        {
            throw new ArgumentException(
                "Verify every reported pole exactly once with a reason.", nameof(findings));
        }

        ManagerFindings = JsonSerializer.SerializeToDocument(findings.Select(item =>
            new VerifiedPoleIssue(item.PoleCode.Trim(), item.ConfirmedError, item.Reason.Trim())));
        ManagerVerifiedBy = managerId;
        ManagerVerifiedAt = now;
        ManagerVerificationReason = reason.Trim();
        UpdatedAt = now;
        Version++;
    }

    public void Reject(Guid adminId, string reason, DateTimeOffset now)
    {
        EnsureSubmitted(adminId, reason, now);
        if (ManagerVerifiedAt is null)
        {
            throw new InvalidOperationException("Manager verification is required before a decision.");
        }
        Status = QualityRefundStatus.Rejected;
        SetDecision(adminId, reason, now);
    }

    public void Approve(Guid adminId, string reason, decimal amount, DateTimeOffset now)
    {
        EnsureSubmitted(adminId, reason, now);
        if (ManagerVerifiedAt is null || ManagerFindings is null ||
            !(ManagerFindings.RootElement.Deserialize<VerifiedPoleIssue[]>() ?? [])
                .Any(item => item.ConfirmedError))
        {
            throw new InvalidOperationException("Manager must confirm an error before approval.");
        }
        if (amount <= 0 || amount != decimal.Truncate(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund must be a positive whole-VND amount.");
        }

        Status = QualityRefundStatus.Approved;
        ApprovedAmount = amount;
        SetDecision(adminId, reason, now);
    }

    public void RecordTransfer(
        Guid adminId,
        string bankTransferReference,
        DateTimeOffset recordedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.NotEmpty(adminId);
        DomainGuard.Utc(recordedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(bankTransferReference);
        if (Status != QualityRefundStatus.Approved ||
            recordedAt < DecidedAt ||
            bankTransferReference.Trim().Length > 150)
        {
            throw new InvalidOperationException("The approved refund or bank transfer reference is invalid.");
        }

        Status = QualityRefundStatus.Refunded;
        BankTransferReference = bankTransferReference.Trim();
        TransferredAt = recordedAt;
        RecordedBy = adminId;
        RecordedAt = recordedAt;
        UpdatedAt = recordedAt;
        Version++;
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException("Refund case version conflict.");
        }
    }

    private void EnsureSubmitted(Guid adminId, string reason, DateTimeOffset now)
    {
        DomainGuard.NotEmpty(adminId);
        DomainGuard.Utc(now);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != QualityRefundStatus.Submitted || reason.Trim().Length > 2000 || now < UpdatedAt)
        {
            throw new InvalidOperationException("Only a submitted report can be decided.");
        }
    }

    private void SetDecision(Guid adminId, string reason, DateTimeOffset now)
    {
        DecidedBy = adminId;
        DecidedAt = now;
        DecisionReason = reason.Trim();
        UpdatedAt = now;
        Version++;
    }
}
