using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyFundingAllocation : Entity
{
    private SurveyFundingAllocation() { }

    public Guid FundingReceiptId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid DepositObligationId { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset AllocatedAt { get; private set; }

    public static SurveyFundingAllocation Create(
        Guid id,
        Guid fundingReceiptId,
        Guid surveyOrderId,
        Guid depositObligationId,
        decimal allocatedAmount,
        string currency,
        DateTimeOffset allocatedAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(fundingReceiptId, nameof(fundingReceiptId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(depositObligationId, nameof(depositObligationId));
        DomainGuard.Utc(allocatedAt, nameof(allocatedAt));

        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(allocatedAmount, nameof(allocatedAmount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        return new SurveyFundingAllocation
        {
            Id = id,
            FundingReceiptId = fundingReceiptId,
            SurveyOrderId = surveyOrderId,
            DepositObligationId = depositObligationId,
            AllocatedAmount = validatedAmount,
            Currency = validatedCurrency,
            AllocatedAt = allocatedAt
        };
    }
}
