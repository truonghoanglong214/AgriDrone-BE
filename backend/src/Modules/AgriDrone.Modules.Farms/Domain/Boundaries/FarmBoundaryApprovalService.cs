using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Boundaries;

public static class FarmBoundaryApprovalService
{
    public static void Approve(
        FarmBoundary draft,
        FarmBoundary? currentApproved,
        IEnumerable<FarmZone> farmZones,
        Guid reviewedBy,
        string reason,
        DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(farmZones);
        DomainGuard.NotEmpty(reviewedBy);
        DomainGuard.Utc(reviewedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var activeZones = farmZones
            .Where(zone => !zone.IsArchived && zone.Status == GeneralStatus.Active)
            .ToArray();

        draft.EnsureCanApprove(reviewedBy, reason, reviewedAt);
        EnsureReplacementMatches(draft, currentApproved);
        currentApproved?.EnsureCanSupersede(draft.Id, reviewedAt);
        EnsureZonesFitBoundary(draft, activeZones);

        currentApproved?.Supersede(draft.Id, reviewedAt);
        draft.Approve(reviewedBy, reason, reviewedAt);
    }

    private static void EnsureReplacementMatches(
        FarmBoundary draft,
        FarmBoundary? currentApproved)
    {
        if (currentApproved is null)
        {
            return;
        }

        if (currentApproved.Status != FarmBoundaryStatus.Approved ||
            currentApproved.TenantId != draft.TenantId ||
            currentApproved.FarmId != draft.FarmId ||
            currentApproved.VersionNumber >= draft.VersionNumber)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.ReplacementMismatch,
                "The replacement must be a newer boundary version for the same tenant and Farm.");
        }
    }

    private static void EnsureZonesFitBoundary(
        FarmBoundary draft,
        FarmZone[] activeZones)
    {
        foreach (var zone in activeZones)
        {
            if (zone.FarmId != draft.FarmId)
            {
                throw new FarmBoundaryDomainException(
                    FarmBoundaryDomainErrorCodes.ReplacementMismatch,
                    "Every Zone used for approval must belong to the same Farm.");
            }

            if (zone.Boundary is null)
            {
                throw new FarmBoundaryDomainException(
                    FarmBoundaryDomainErrorCodes.ZoneBoundaryRequired,
                    "Every active Zone must have a boundary before FarmBoundary approval.");
            }

            if (!draft.Geometry.Covers(zone.Boundary))
            {
                throw new FarmBoundaryDomainException(
                    FarmBoundaryDomainErrorCodes.ZoneOutsideBoundary,
                    "Every active Zone must be covered by the FarmBoundary.");
            }
        }

        for (var leftIndex = 0; leftIndex < activeZones.Length; leftIndex++)
        {
            var left = activeZones[leftIndex].Boundary!;
            for (var rightIndex = leftIndex + 1;
                 rightIndex < activeZones.Length;
                 rightIndex++)
            {
                var right = activeZones[rightIndex].Boundary!;
                if (left.Intersects(right) && !left.Touches(right))
                {
                    throw new FarmBoundaryDomainException(
                        FarmBoundaryDomainErrorCodes.ZonesOverlap,
                        "Active Zone boundaries cannot overlap.");
                }
            }
        }
    }
}
