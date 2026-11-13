using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRequestQuoteSnapshot : Entity
{
    private SurveyRequestQuoteSnapshot() { }

    public Guid SurveyRequestId { get; private set; }
    public Guid BusinessPolicyVersionId { get; private set; }
    public string PolicyFingerprint { get; private set; } = null!;
    public Guid SurveyServiceRevisionId { get; private set; }
    public int SurveyServiceRevisionVersion { get; private set; }
    public Guid PriceVersionId { get; private set; }
    public int PriceVersionNumber { get; private set; }
    public decimal PricePerPole { get; private set; }
    public string Currency { get; private set; } = null!;
    public Guid DepositPolicyId { get; private set; }
    public int DepositVersionNumber { get; private set; }
    public decimal DepositAmount { get; private set; }
    public int? EstimatedPoleCount { get; private set; }
    public decimal? IndicativeTotalAmount { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyRequestQuoteSnapshot Create(
        Guid id,
        Guid surveyRequestId,
        Guid businessPolicyVersionId,
        string policyFingerprint,
        Guid surveyServiceRevisionId,
        int surveyServiceRevisionVersion,
        Guid priceVersionId,
        int priceVersionNumber,
        decimal pricePerPole,
        string currency,
        Guid depositPolicyId,
        int depositVersionNumber,
        decimal depositAmount,
        int? estimatedPoleCount,
        DateTimeOffset evaluatedAt,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyRequestId, nameof(surveyRequestId));
        DomainGuard.NotEmpty(businessPolicyVersionId, nameof(businessPolicyVersionId));
        DomainGuard.NotEmpty(surveyServiceRevisionId, nameof(surveyServiceRevisionId));
        DomainGuard.NotEmpty(priceVersionId, nameof(priceVersionId));
        DomainGuard.NotEmpty(depositPolicyId, nameof(depositPolicyId));
        DomainGuard.Utc(evaluatedAt, nameof(evaluatedAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var validatedPrice = PolicyValidationGuard.ValidatePositiveWholeVnd(pricePerPole, nameof(pricePerPole));
        var validatedDeposit = PolicyValidationGuard.ValidatePositiveWholeVnd(depositAmount, nameof(depositAmount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(CurrencyCode.Create(currency), nameof(currency));
        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(policyFingerprint, 64, nameof(policyFingerprint));

        decimal? indicativeTotal = null;
        if (estimatedPoleCount.HasValue)
        {
            if (estimatedPoleCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(estimatedPoleCount),
                    "Estimated pole count must be positive when provided.");
            }

            indicativeTotal = estimatedPoleCount.Value * validatedPrice;
        }

        return new SurveyRequestQuoteSnapshot
        {
            Id = id,
            SurveyRequestId = surveyRequestId,
            BusinessPolicyVersionId = businessPolicyVersionId,
            PolicyFingerprint = normalizedFingerprint,
            SurveyServiceRevisionId = surveyServiceRevisionId,
            SurveyServiceRevisionVersion = surveyServiceRevisionVersion,
            PriceVersionId = priceVersionId,
            PriceVersionNumber = priceVersionNumber,
            PricePerPole = validatedPrice,
            Currency = validatedCurrency,
            DepositPolicyId = depositPolicyId,
            DepositVersionNumber = depositVersionNumber,
            DepositAmount = validatedDeposit,
            EstimatedPoleCount = estimatedPoleCount,
            IndicativeTotalAmount = indicativeTotal,
            EvaluatedAt = evaluatedAt,
            CreatedAt = createdAt
        };
    }
}
