using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRefundObligation : Entity
{
    private SurveyRefundObligation() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public SurveyRefundOriginKind OriginKind { get; private set; }
    public Guid OriginDecisionId { get; private set; }
    public Guid? FundingReceiptId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public SurveyRefundObligationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ReservedAt { get; private set; }
    public DateTimeOffset? ExecutingAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public static SurveyRefundObligation Create(
        Guid id,
        Guid surveyOrderId,
        Guid tenantId,
        SurveyRefundOriginKind originKind,
        Guid originDecisionId,
        decimal amount,
        string currency,
        DateTimeOffset createdAt,
        Guid? fundingReceiptId = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(originDecisionId, nameof(originDecisionId));
        if (fundingReceiptId.HasValue)
        {
            DomainGuard.NotEmpty(fundingReceiptId.Value, nameof(fundingReceiptId));
        }
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        return new SurveyRefundObligation
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            OriginKind = originKind,
            OriginDecisionId = originDecisionId,
            FundingReceiptId = fundingReceiptId,
            Amount = validatedAmount,
            Currency = validatedCurrency,
            Status = SurveyRefundObligationStatus.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void MarkReserved(DateTimeOffset timestamp)
    {
        DomainGuard.Utc(timestamp, nameof(timestamp));
        EnsureNotFinal();
        if (Status != SurveyRefundObligationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot reserve refund obligation in status {Status}.");
        }

        Status = SurveyRefundObligationStatus.Reserved;
        ReservedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void MarkExecuting(DateTimeOffset timestamp)
    {
        DomainGuard.Utc(timestamp, nameof(timestamp));
        EnsureNotFinal();
        if (Status != SurveyRefundObligationStatus.Reserved)
        {
            throw new InvalidOperationException($"Cannot execute refund obligation in status {Status}.");
        }

        Status = SurveyRefundObligationStatus.Executing;
        ExecutingAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void MarkCompleted(DateTimeOffset timestamp)
    {
        DomainGuard.Utc(timestamp, nameof(timestamp));
        EnsureNotFinal();
        if (Status != SurveyRefundObligationStatus.Executing)
        {
            throw new InvalidOperationException($"Cannot complete refund obligation in status {Status}.");
        }

        Status = SurveyRefundObligationStatus.Completed;
        CompletedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public void Cancel(string reason, DateTimeOffset timestamp)
    {
        DomainGuard.Utc(timestamp, nameof(timestamp));
        EnsureNotFinal();
        PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));

        Status = SurveyRefundObligationStatus.Cancelled;
        CancelledAt = timestamp;
        UpdatedAt = timestamp;
    }

    private void EnsureNotFinal()
    {
        if (Status is SurveyRefundObligationStatus.Completed or SurveyRefundObligationStatus.Cancelled)
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.RefundObligationAlreadyFinal,
                $"Refund obligation {Id} is already final in status {Status}.");
        }
    }
}
