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
        long version)
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
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? ScientificName { get; private set; }

    public ConditionType ConditionType { get; private set; }

    public string? Description { get; private set; }

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
        DateTimeOffset createdAt)
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
            1
        );
    }

    public PlantCondition CreateNextRevision(
        string name,
        string? scientificName,
        string? description,
        DateTimeOffset createdAt)
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
            Version = 1
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
}
