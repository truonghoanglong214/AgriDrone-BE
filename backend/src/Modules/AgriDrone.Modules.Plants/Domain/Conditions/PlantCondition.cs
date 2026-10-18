using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Conditions;

public sealed class PlantCondition : Entity
{
    private PlantCondition()
    {
    }

    private PlantCondition(
        Guid id,
        string code,
        string name,
        string? scientificName,
        ConditionType conditionType,
        string? description,
        int revisionNumber,
        bool isActive,
        DateTimeOffset createdAt,
        long version,
        PlantHealthLabelKind? plantHealthLabelKind = null,
        string? datasetVersion = null,
        string? annotationRuleVersion = null,
        string? teamLabelLimitation = null)
    {
        Id = id;
        Code = code;
        Name = name;
        ScientificName = scientificName;
        ConditionType = conditionType;
        Description = description;
        RevisionNumber = revisionNumber;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Version = version;
        PlantHealthLabelKind = plantHealthLabelKind;
        DatasetVersion = datasetVersion;
        AnnotationRuleVersion = annotationRuleVersion;
        TeamLabelLimitation = teamLabelLimitation;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? ScientificName { get; private set; }

    public ConditionType ConditionType { get; private set; }

    public string? Description { get; private set; }

    public PlantHealthLabelKind? PlantHealthLabelKind { get; private set; }

    public string? DatasetVersion { get; private set; }

    public string? AnnotationRuleVersion { get; private set; }

    public string? TeamLabelLimitation { get; private set; }

    public int RevisionNumber { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public long Version { get; private set; } = 1;

    public ICollection<ConditionDetection> Detections { get; private set; } = [];

    public ICollection<ConditionDetectionReview> CorrectedReviews { get; private set; } = [];

    public static PlantCondition Create(
        string code,
        string name,
        string? scientificName,
        ConditionType conditionType,
        string? description,
        DateTimeOffset createdAt,
        PlantHealthLabelKind? plantHealthLabelKind = null,
        string? datasetVersion = null,
        string? annotationRuleVersion = null,
        string? teamLabelLimitation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        DomainGuard.Utc(createdAt);

        if (!Enum.IsDefined(conditionType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(conditionType),
                conditionType,
                "Condition type is not supported.");
        }

        var targetMetadata = NormalizePlantHealthMetadata(
            code,
            conditionType,
            plantHealthLabelKind,
            datasetVersion,
            annotationRuleVersion,
            teamLabelLimitation);

        return new PlantCondition
        (
            Guid.NewGuid(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            NormalizeOptional(scientificName),
            conditionType,
            NormalizeOptional(description),
            1,
            true,
            createdAt,
            1,
            targetMetadata.LabelKind,
            targetMetadata.DatasetVersion,
            targetMetadata.AnnotationRuleVersion,
            targetMetadata.TeamLabelLimitation
        );
    }

    public PlantCondition CreateNextRevision(
        string name,
        string? scientificName,
        string? description,
        DateTimeOffset createdAt,
        string? datasetVersion = null,
        string? annotationRuleVersion = null,
        string? teamLabelLimitation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        DomainGuard.Utc(createdAt);

        if (!IsActive)
        {
            throw new InvalidOperationException(
                "Only an active plant condition can be versioned.");
        }

        if (createdAt < CreatedAt)
        {
            throw new ArgumentException(
                "The next revision cannot be created before the current revision.",
                nameof(createdAt));
        }

        var nextRevisionNumber = checked(RevisionNumber + 1);
        var targetMetadata = NormalizePlantHealthMetadata(
            Code,
            ConditionType,
            PlantHealthLabelKind,
            datasetVersion ?? DatasetVersion,
            annotationRuleVersion ?? AnnotationRuleVersion,
            teamLabelLimitation ?? TeamLabelLimitation);

        Retire(createdAt);

        return new PlantCondition
        {
            Id = Guid.NewGuid(),
            Code = Code,
            Name = name.Trim(),
            ScientificName = NormalizeOptional(scientificName),
            ConditionType = ConditionType,
            Description = NormalizeOptional(description),
            RevisionNumber = nextRevisionNumber,
            SupersedesId = Id,
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Version = 1,
            PlantHealthLabelKind = targetMetadata.LabelKind,
            DatasetVersion = targetMetadata.DatasetVersion,
            AnnotationRuleVersion = targetMetadata.AnnotationRuleVersion,
            TeamLabelLimitation = targetMetadata.TeamLabelLimitation
        };
    }

    public bool Retire(DateTimeOffset retiredAt)
    {
        DomainGuard.Utc(retiredAt);

        if (!IsActive)
        {
            return false;
        }

        if (retiredAt < CreatedAt)
        {
            throw new ArgumentException(
                "Retirement time cannot be earlier than creation time.",
                nameof(retiredAt));
        }

        IsActive = false;
        RetiredAt = retiredAt;
        UpdatedAt = retiredAt;
        Version++;

        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static PlantHealthMetadata NormalizePlantHealthMetadata(
        string code,
        ConditionType conditionType,
        PlantHealthLabelKind? labelKind,
        string? datasetVersion,
        string? annotationRuleVersion,
        string? teamLabelLimitation)
    {
        if (!labelKind.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(datasetVersion) ||
                !string.IsNullOrWhiteSpace(annotationRuleVersion) ||
                !string.IsNullOrWhiteSpace(teamLabelLimitation))
            {
                throw new ArgumentException(
                    "Plant Health provenance requires a label kind.");
            }

            return new PlantHealthMetadata(null, null, null, null);
        }

        if (!Enum.IsDefined(labelKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(labelKind));
        }

        if (!PlantHealthLabelPolicy.IsAllowed(code, labelKind.Value))
        {
            throw new ArgumentException(
                $"Condition code '{code.Trim().ToUpperInvariant()}' is not in the locked Plant Health label set.",
                nameof(code));
        }

        if (labelKind == global::AgriDrone.Modules.Plants.Domain.Conditions.PlantHealthLabelKind.SuspectedDisease &&
            conditionType != ConditionType.Disease)
        {
            throw new ArgumentException(
                "A suspected-disease label must use the Disease condition type.");
        }

        if (labelKind == global::AgriDrone.Modules.Plants.Domain.Conditions.PlantHealthLabelKind.ConfusingNonDisease &&
            conditionType == ConditionType.Disease)
        {
            throw new ArgumentException(
                "A confusing/non-disease label cannot use the Disease condition type.");
        }

        var normalizedDatasetVersion = NormalizeRequired(
            datasetVersion,
            100,
            nameof(datasetVersion));
        var normalizedAnnotationRuleVersion = NormalizeRequired(
            annotationRuleVersion,
            100,
            nameof(annotationRuleVersion));
        var normalizedTeamLabelLimitation = NormalizeRequired(
            teamLabelLimitation,
            1000,
            nameof(teamLabelLimitation));

        if (normalizedDatasetVersion != PlantHealthLabelPolicy.DatasetVersion ||
            normalizedAnnotationRuleVersion != PlantHealthLabelPolicy.AnnotationRuleVersion ||
            normalizedTeamLabelLimitation != PlantHealthLabelPolicy.TeamLabelLimitation)
        {
            throw new ArgumentException(
                "Plant Health provenance must match the locked pilot policy.");
        }

        return new PlantHealthMetadata(
            labelKind,
            normalizedDatasetVersion,
            normalizedAnnotationRuleVersion,
            normalizedTeamLabelLimitation);
    }

    private static string NormalizeRequired(
        string? value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }

    private sealed record PlantHealthMetadata(
        PlantHealthLabelKind? LabelKind,
        string? DatasetVersion,
        string? AnnotationRuleVersion,
        string? TeamLabelLimitation);
}
