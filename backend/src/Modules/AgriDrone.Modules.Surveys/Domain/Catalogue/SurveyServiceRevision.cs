using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyServiceRevision : Entity
{
    private SurveyServiceRevision() { }

    public Guid SurveyServiceId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool MapsPoles { get; private set; }
    public bool ScreensDisease { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static SurveyServiceRevision Create(
        Guid id,
        Guid surveyServiceId,
        int versionNumber,
        Guid? predecessorVersionId,
        string name,
        string description,
        bool mapsPoles,
        bool screensDisease,
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

        if (!mapsPoles)
        {
            throw new ArgumentException(
                "MapsPoles capability must be enabled. Dragon fruit disease screening requires pole mapping.",
                nameof(mapsPoles));
        }

        var normalizedName = PolicyValidationGuard.NormalizeRequired(name, 150, nameof(name));
        var normalizedDescription = PolicyValidationGuard.NormalizeRequired(description, 4000, nameof(description));
        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        return new SurveyServiceRevision
        {
            Id = id,
            SurveyServiceId = surveyServiceId,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            Name = normalizedName,
            Description = normalizedDescription,
            MapsPoles = mapsPoles,
            ScreensDisease = screensDisease,
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
                "A closed service revision effective window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.InvalidEffectiveWindow,
                "Service revision effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
