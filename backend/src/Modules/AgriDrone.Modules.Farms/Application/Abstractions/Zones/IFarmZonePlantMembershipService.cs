using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Zones;

public interface IFarmZonePlantMembershipService
{
    Task<int> RecomputeAutoAsync(Guid farmId, Guid zoneId, Polygon boundary,
        Guid actorId, DateTimeOffset changedAt, CancellationToken cancellationToken);
    Task<int> CloseZoneAsync(Guid farmId, Guid zoneId, Guid actorId,
        DateTimeOffset changedAt, CancellationToken cancellationToken);
    Task<Result<ZonePlantMembershipChangeResult>> ApplyManualAsync(
        Guid farmId, Guid zoneId, IReadOnlyList<ZonePlantMembershipOperation> operations,
        Guid actorId, string reason, DateTimeOffset changedAt,
        CancellationToken cancellationToken);
}
