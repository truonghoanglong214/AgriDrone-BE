using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class HarvestReadinessCriterion : AggregateRoot
{
    private HarvestReadinessCriterion() { }

    public string Code { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public HarvestReadinessGranularity Granularity { get; private set; }
    public HarvestReadinessCriterionStatus Status { get; private set; }
    public JsonDocument ObservableIndicators { get; private set; } = null!;
    public string? GroundTruthProtocol { get; private set; }
    public string? DatasetRequirements { get; private set; }
    public string? EvaluationProtocol { get; private set; }
    public string? ValidationEvidenceReference { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid? SupersedesCriterionId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ValidatedBy { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public Guid? RetiredBy { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public HarvestReadinessCriterion? SupersedesCriterion { get; private set; }

    public static HarvestReadinessCriterion CreateExperimental(
        string code,
        string name,
        string description,
        HarvestReadinessGranularity granularity,
        JsonDocument observableIndicators,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt) =>
        CreateVersion(
            code,
            1,
            name,
            description,
            granularity,
            observableIndicators,
            effectiveFrom,
            effectiveTo,
            supersedesCriterionId: null,
            createdBy,
            createdAt);

    public HarvestReadinessCriterion CreateNextVersion(
        string name,
        string description,
        HarvestReadinessGranularity granularity,
        JsonDocument observableIndicators,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status == HarvestReadinessCriterionStatus.Retired)
        {
            throw InvalidTransition(Status);
        }

        DomainGuard.Utc(effectiveFrom);
        EnsureTimestamp(createdAt);
        if (effectiveFrom <= EffectiveFrom)
        {
            throw new ArgumentException(
                "A replacement must become effective after the current version.",
                nameof(effectiveFrom));
        }

        if (EffectiveTo.HasValue && EffectiveTo.Value != effectiveFrom)
        {
            throw new ArgumentException(
                "The replacement effective time must match the current version's closing boundary.",
                nameof(effectiveFrom));
        }

        var replacement = CreateVersion(
            Code,
            checked(VersionNumber + 1),
            name,
            description,
            granularity,
            observableIndicators,
            effectiveFrom,
            effectiveTo,
            Id,
            createdBy,
            createdAt);

        EffectiveTo ??= effectiveFrom;
        UpdatedAt = createdAt;
        return replacement;
    }

    private static HarvestReadinessCriterion CreateVersion(
        string code,
        int versionNumber,
        string name,
        string description,
        HarvestReadinessGranularity granularity,
        JsonDocument observableIndicators,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? supersedesCriterionId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        var normalizedCode = NormalizeRequired(code, 80, nameof(code))
            .ToUpperInvariant();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        if (!Enum.IsDefined(granularity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(granularity),
                granularity,
                "Harvest-readiness granularity is not supported.");
        }

        ArgumentNullException.ThrowIfNull(observableIndicators);
        if (observableIndicators.RootElement.ValueKind != JsonValueKind.Array ||
            observableIndicators.RootElement.GetArrayLength() == 0)
        {
            throw new ArgumentException(
                "Observable indicators must be a non-empty JSON array.",
                nameof(observableIndicators));
        }

        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);
        DomainGuard.Utc(createdAt);
        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value);
            if (effectiveTo <= effectiveFrom)
            {
                throw new ArgumentException(
                    "EffectiveTo must be later than EffectiveFrom.",
                    nameof(effectiveTo));
            }
        }

        ValidateVersionLineage(versionNumber, supersedesCriterionId);

        return new HarvestReadinessCriterion
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            VersionNumber = versionNumber,
            Name = NormalizeRequired(name, 200, nameof(name)),
            Description = NormalizeRequired(description, 4000, nameof(description)),
            Granularity = granularity,
            Status = HarvestReadinessCriterionStatus.Experimental,
            ObservableIndicators = Clone(observableIndicators),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SupersedesCriterionId = supersedesCriterionId,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Validate(
        string groundTruthProtocol,
        string datasetRequirements,
        string evaluationProtocol,
        string validationEvidenceReference,
        Guid validatedBy,
        DateTimeOffset validatedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureStatus(HarvestReadinessCriterionStatus.Experimental);
        DomainGuard.NotEmpty(validatedBy);
        EnsureTimestamp(validatedAt);

        GroundTruthProtocol = NormalizeEvidence(
            groundTruthProtocol,
            4000,
            nameof(groundTruthProtocol));
        DatasetRequirements = NormalizeEvidence(
            datasetRequirements,
            4000,
            nameof(datasetRequirements));
        EvaluationProtocol = NormalizeEvidence(
            evaluationProtocol,
            4000,
            nameof(evaluationProtocol));
        ValidationEvidenceReference = NormalizeEvidence(
            validationEvidenceReference,
            1000,
            nameof(validationEvidenceReference));
        ValidatedBy = validatedBy;
        ValidatedAt = validatedAt;
        Status = HarvestReadinessCriterionStatus.Validated;
        UpdatedAt = validatedAt;
    }

    public void Retire(
        Guid retiredBy,
        DateTimeOffset retiredAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status == HarvestReadinessCriterionStatus.Retired)
        {
            throw InvalidTransition(Status);
        }

        DomainGuard.NotEmpty(retiredBy);
        EnsureTimestamp(retiredAt);
        RetiredBy = retiredBy;
        RetiredAt = retiredAt;
        Status = HarvestReadinessCriterionStatus.Retired;
        UpdatedAt = retiredAt;
    }

    public bool IsAvailableAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt);
        return Status != HarvestReadinessCriterionStatus.Retired &&
               EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new HarvestReadinessCriterionDomainException(
                HarvestReadinessCriterionErrorCodes.VersionConflict,
                $"Expected criterion version {expectedVersion}, but found {Version}.");
        }
    }

    private void EnsureStatus(HarvestReadinessCriterionStatus expected)
    {
        if (Status != expected)
        {
            throw InvalidTransition(Status);
        }
    }

    private static HarvestReadinessCriterionDomainException InvalidTransition(
        HarvestReadinessCriterionStatus current) =>
        new(
            HarvestReadinessCriterionErrorCodes.InvalidLifecycleTransition,
            $"Harvest-readiness criterion cannot transition from {current}.");

    private void EnsureTimestamp(DateTimeOffset occurredAt)
    {
        DomainGuard.Utc(occurredAt);
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentException(
                "Lifecycle time cannot be earlier than the last update.",
                nameof(occurredAt));
        }
    }

    private static void ValidateVersionLineage(
        int versionNumber,
        Guid? supersedesCriterionId)
    {
        if (supersedesCriterionId == Guid.Empty ||
            (versionNumber == 1 && supersedesCriterionId.HasValue) ||
            (versionNumber > 1 && !supersedesCriterionId.HasValue))
        {
            throw new ArgumentException(
                "Criterion version lineage is invalid.",
                nameof(supersedesCriterionId));
        }
    }

    private static string NormalizeEvidence(
        string value,
        int maximumLength,
        string parameterName)
    {
        try
        {
            return NormalizeRequired(value, maximumLength, parameterName);
        }
        catch (ArgumentException exception)
        {
            throw new HarvestReadinessCriterionDomainException(
                HarvestReadinessCriterionErrorCodes.ValidationEvidenceRequired,
                exception.Message);
        }
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static JsonDocument Clone(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());
}
