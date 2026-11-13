using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyDepositPolicy : Entity
{
    private SurveyDepositPolicy() { }

    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static SurveyDepositPolicy Create(
        Guid id,
        int versionNumber,
        Guid? predecessorVersionId,
        decimal amount,
        CurrencyCode currency,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);
        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(currency, nameof(currency));

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        return new SurveyDepositPolicy
        {
            Id = id,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            Amount = validatedAmount,
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
        DomainGuard.Utc(evaluatedAt, nameof(evaluatedAt));
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo, nameof(effectiveTo));
        if (EffectiveTo.HasValue)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.WindowAlreadyClosed,
                "A closed deposit policy effective window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.InvalidEffectiveWindow,
                "Deposit policy effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
