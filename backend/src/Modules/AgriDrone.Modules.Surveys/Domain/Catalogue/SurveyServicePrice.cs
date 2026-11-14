using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyServicePrice : Entity
{
    private SurveyServicePrice() { }

    public Guid SurveyServiceId { get; private set; }
    public int? VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public PricePerPole? PricePerPole { get; private set; }
    public decimal? MappingPricePerPole { get; private set; }
    public decimal? PlantHealthPricePerPole { get; private set; }
    public decimal? PricePerHa { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? EvidenceReference { get; private set; }
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
        DateTimeOffset createdAt,
        decimal? mappingPricePerPole = null,
        decimal? plantHealthPricePerPole = null)
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

        if (mappingPricePerPole.HasValue != plantHealthPricePerPole.HasValue ||
            (mappingPricePerPole.HasValue &&
             (mappingPricePerPole <= 0m || plantHealthPricePerPole <= 0m ||
              mappingPricePerPole != decimal.Truncate(mappingPricePerPole.Value) ||
              plantHealthPricePerPole != decimal.Truncate(plantHealthPricePerPole!.Value) ||
              mappingPricePerPole + plantHealthPricePerPole != pricePerPole.Amount)))
            throw new ArgumentException("Combo prices require both positive whole-VND component rates; their sum must equal AmountPerPole.");

        var validatedPrice = global::AgriDrone.Modules.Surveys.Domain.PricePerPole
            .Create(pricePerPole.Amount);

        return new SurveyServicePrice
        {
            Id = Guid.NewGuid(),
            SurveyServiceId = surveyServiceId,
            PricePerPole = validatedPrice,
            MappingPricePerPole = mappingPricePerPole,
            PlantHealthPricePerPole = plantHealthPricePerPole,
            PricePerHa = null,
            Currency = currency.Value,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public static SurveyServicePrice CreatePerPoleV3(
        Guid id,
        Guid surveyServiceId,
        int versionNumber,
        Guid? predecessorVersionId,
        decimal pricePerPoleAmount,
        CurrencyCode currency,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyServiceId, nameof(surveyServiceId));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);
        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(pricePerPoleAmount, nameof(pricePerPoleAmount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(currency, nameof(currency));

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        var validatedPrice = global::AgriDrone.Modules.Surveys.Domain.PricePerPole.Create(validatedAmount);

        return new SurveyServicePrice
        {
            Id = id,
            SurveyServiceId = surveyServiceId,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            PricePerPole = validatedPrice,
            PricePerHa = null,
            Currency = validatedCurrency,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReasonCode = normalizedReasonCode,
            EvidenceReference = normalizedEvidence
        };
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt);
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo);
        if (EffectiveTo.HasValue)
        {
            throw new SurveyDomainException(
                SurveyServiceDomainErrorCodes.PriceWindowAlreadyClosed,
                "A closed price window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                SurveyServiceDomainErrorCodes.InvalidPriceWindow,
                "Price effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
