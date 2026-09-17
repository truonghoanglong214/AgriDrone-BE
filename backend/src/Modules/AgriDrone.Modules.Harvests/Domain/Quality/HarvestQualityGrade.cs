using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Harvests.Domain.Quality;

public sealed class HarvestQualityGrade : Entity
{
    private HarvestQualityGrade()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public int RevisionNumber { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public long Version { get; private set; } = 1;

    public DateTimeOffset? RetiredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<PlantHarvestQualityDetail> PlantHarvestQualityDetails { get; private set; } = [];

    public static HarvestQualityGrade Create(
        string code,
        string name,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        DomainGuard.Utc(createdAt);

        if(displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order must be a non-negative integer.");
        }

        return new HarvestQualityGrade
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            DisplayOrder = displayOrder,
            RevisionNumber = 1,
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Version = 1

        };
    }

    public HarvestQualityGrade CreateNextVersion(
        string name,
        int displayOrder,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        DomainGuard.Utc(createdAt);

        if(!IsActive)
        {
            throw new InvalidOperationException("Cannot create a new version of an inactive quality grade.");
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order must be a non-negative integer.");
        }

        if (createdAt < CreatedAt)
        {
            throw new ArgumentException(
                "The next revision cannot be created before the current revision.",
                nameof(createdAt));
        }

        var nextVersion = checked(RevisionNumber + 1);

        Retire(createdAt);
        return new HarvestQualityGrade
        {
            Id = Guid.NewGuid(),
            Code = Code,
            Name = name.Trim(),
            DisplayOrder = displayOrder,
            RevisionNumber = nextVersion,
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
}
