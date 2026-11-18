using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyDepositObligationStatus
{
    Pending,
    Satisfied,
    Cancelled
}

public sealed class SurveyDepositObligation : Entity
{
    private SurveyDepositObligation() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid BusinessPolicyVersionId { get; private set; }
    public Guid DepositPolicyId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal AllocatedAmount { get; private set; }
    public SurveyDepositObligationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SatisfiedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public bool IsSatisfied => AllocatedAmount >= Amount;
    public decimal RemainingAmount => Math.Max(0m, Amount - AllocatedAmount);

    public static SurveyDepositObligation Create(
        Guid id,
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid businessPolicyVersionId,
        Guid depositPolicyId,
        decimal amount,
        string currency,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(businessPolicyVersionId, nameof(businessPolicyVersionId));
        DomainGuard.NotEmpty(depositPolicyId, nameof(depositPolicyId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        return new SurveyDepositObligation
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            FarmId = farmId,
            BusinessPolicyVersionId = businessPolicyVersionId,
            DepositPolicyId = depositPolicyId,
            Amount = validatedAmount,
            Currency = validatedCurrency,
            AllocatedAmount = 0m,
            Status = SurveyDepositObligationStatus.Pending,
            CreatedAt = createdAt
        };
    }

    public void ApplyAllocation(decimal amount, DateTimeOffset allocatedAt)
    {
        DomainGuard.Utc(allocatedAt, nameof(allocatedAt));
        if (Status == SurveyDepositObligationStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot allocate to a cancelled deposit obligation.");
        }

        if (amount <= 0 || decimal.Floor(amount) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Allocation amount must be a positive whole VND value.");
        }

        AllocatedAmount += amount;
        if (IsSatisfied && Status != SurveyDepositObligationStatus.Satisfied)
        {
            Status = SurveyDepositObligationStatus.Satisfied;
            SatisfiedAt = allocatedAt;
        }
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        DomainGuard.Utc(cancelledAt, nameof(cancelledAt));
        if (Status == SurveyDepositObligationStatus.Cancelled)
        {
            return;
        }

        Status = SurveyDepositObligationStatus.Cancelled;
        CancelledAt = cancelledAt;
    }
}
