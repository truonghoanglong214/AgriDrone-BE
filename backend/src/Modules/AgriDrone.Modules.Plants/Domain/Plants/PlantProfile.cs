using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Plants;

public sealed class PlantProfile : AggregateRoot
{
    private PlantProfile() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public string PhysicalPoleIdentifier { get; private set; } = null!;
    public Point BaselineLocation { get; private set; } = null!;
    public Point CurrentLocation { get; private set; } = null!;
    public Guid BaselineMapVersionId { get; private set; }
    public Guid CurrentMapVersionId { get; private set; }
    public PlantProfileStatus Status { get; private set; }
    public Guid? PredecessorProfileId { get; private set; }
    public string? ReplacementReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PlantProfile Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        string physicalPoleIdentifier,
        Point baselineLocation,
        Guid baselineMapVersionId,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(baselineMapVersionId, nameof(baselineMapVersionId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedIdentifier = NormalizeIdentifier(physicalPoleIdentifier);
        ArgumentNullException.ThrowIfNull(baselineLocation, nameof(baselineLocation));
        if (baselineLocation.SRID != 4326)
        {
            throw new ArgumentException("Location geometry must have SRID 4326.", nameof(baselineLocation));
        }

        return new PlantProfile
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            PhysicalPoleIdentifier = normalizedIdentifier,
            BaselineLocation = (Point)baselineLocation.Copy(),
            CurrentLocation = (Point)baselineLocation.Copy(),
            BaselineMapVersionId = baselineMapVersionId,
            CurrentMapVersionId = baselineMapVersionId,
            Status = PlantProfileStatus.Active,
            PredecessorProfileId = null,
            ReplacementReason = null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Relocate(
        Point newLocation,
        Guid mapVersionId,
        double distanceM,
        double matchingDistanceThresholdM,
        DateTimeOffset at)
    {
        DomainGuard.Utc(at, nameof(at));
        DomainGuard.NotEmpty(mapVersionId, nameof(mapVersionId));
        ArgumentNullException.ThrowIfNull(newLocation, nameof(newLocation));
        if (newLocation.SRID != 4326)
        {
            throw new ArgumentException("Location geometry must have SRID 4326.", nameof(newLocation));
        }

        if (Status != PlantProfileStatus.Active)
        {
            throw new InvalidOperationException($"Cannot relocate plant profile in status {Status}.");
        }

        if (distanceM > matchingDistanceThresholdM)
        {
            throw new InvalidOperationException(
                $"Relocation distance {distanceM:F2}m exceeds matching threshold {matchingDistanceThresholdM:F2}m. " +
                "A relocation beyond threshold must be handled as replacement or needs manual review.");
        }

        CurrentLocation = (Point)newLocation.Copy();
        CurrentMapVersionId = mapVersionId;
        UpdatedAt = at;
    }

    public PlantProfile ReplaceWithNewProfile(
        Guid newProfileId,
        string newPoleIdentifier,
        Point newLocation,
        Guid mapVersionId,
        string reason,
        DateTimeOffset at)
    {
        DomainGuard.NotEmpty(newProfileId, nameof(newProfileId));
        DomainGuard.Utc(at, nameof(at));
        DomainGuard.NotEmpty(mapVersionId, nameof(mapVersionId));
        ArgumentNullException.ThrowIfNull(newLocation, nameof(newLocation));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Replacement reason is required.", nameof(reason));
        }

        if (Status != PlantProfileStatus.Active && Status != PlantProfileStatus.NeedsReview)
        {
            throw new InvalidOperationException($"Cannot replace plant profile in status {Status}.");
        }

        Status = PlantProfileStatus.Replaced;
        ReplacementReason = reason.Trim();
        UpdatedAt = at;

        return new PlantProfile
        {
            Id = newProfileId,
            TenantId = TenantId,
            FarmId = FarmId,
            PhysicalPoleIdentifier = NormalizeIdentifier(newPoleIdentifier),
            BaselineLocation = (Point)newLocation.Copy(),
            CurrentLocation = (Point)newLocation.Copy(),
            BaselineMapVersionId = mapVersionId,
            CurrentMapVersionId = mapVersionId,
            Status = PlantProfileStatus.Active,
            PredecessorProfileId = Id,
            ReplacementReason = null,
            CreatedAt = at,
            UpdatedAt = at
        };
    }

    public void Retire(string reason, DateTimeOffset at)
    {
        DomainGuard.Utc(at, nameof(at));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Retirement reason is required.", nameof(reason));
        }

        if (Status == PlantProfileStatus.Retired)
        {
            return;
        }

        Status = PlantProfileStatus.Retired;
        ReplacementReason = reason.Trim();
        UpdatedAt = at;
    }

    public void MarkNeedsReview(string reason, DateTimeOffset at)
    {
        DomainGuard.Utc(at, nameof(at));
        Status = PlantProfileStatus.NeedsReview;
        ReplacementReason = reason;
        UpdatedAt = at;
    }

    private static string NormalizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Pole identifier cannot be empty.", nameof(identifier));
        }

        var trimmed = identifier.Trim().ToUpperInvariant();
        if (trimmed.Length > 50)
        {
            throw new ArgumentException("Pole identifier cannot exceed 50 characters.", nameof(identifier));
        }

        return trimmed;
    }
}
