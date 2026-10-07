using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyServicePrice : Entity
{
    private SurveyServicePrice() { }

    public Guid SurveyServiceId { get; private set; }
    public PricePerPole? PricePerPole { get; private set; }
    public decimal? PricePerHa { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyService SurveyService { get; private set; } = null!;

    public bool IsLegacyPerHectarePrice =>
        PricePerPole is null && PricePerHa is > 0;

    public static SurveyServicePrice CreatePerPole(
        Guid surveyServiceId,
        PricePerPole pricePerPole,
        CurrencyCode currency,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyServiceId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);
        DomainGuard.Utc(createdAt);

        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value);
            if (effectiveTo.Value <= effectiveFrom)
            {
                throw new ArgumentException(
                    "Price effective end must be after its start.",
                    nameof(effectiveTo));
            }
        }

        if (currency != CurrencyCode.Vnd)
        {
            throw new ArgumentException(
                "Only VND per-pole prices are supported in the MVP.",
                nameof(currency));
        }

        var validatedPrice = global::AgriDrone.Modules.Surveys.Domain.PricePerPole
            .Create(pricePerPole.Amount);

        return new SurveyServicePrice
        {
            Id = Guid.NewGuid(),
            SurveyServiceId = surveyServiceId,
            PricePerPole = validatedPrice,
            PricePerHa = null,
            Currency = currency.Value,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt);
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }
}
