using AgriDrone.Modules.Farms.Application.Abstractions.Zones;
using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.UnitTests.Application.Farms;

internal sealed class NoOpFarmZonePlantMembershipService
    : IFarmZonePlantMembershipService
{
    public Task<int> RecomputeAutoAsync(
        Guid farmId, Guid zoneId, Polygon boundary, Guid actorId,
        DateTimeOffset changedAt, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<int> CloseZoneAsync(
        Guid farmId, Guid zoneId, Guid actorId,
        DateTimeOffset changedAt, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<Result<ZonePlantMembershipChangeResult>> ApplyManualAsync(
        Guid farmId, Guid zoneId,
        IReadOnlyList<ZonePlantMembershipOperation> operations,
        Guid actorId, string reason, DateTimeOffset changedAt,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(
            new ZonePlantMembershipChangeResult(0, 0)));
}
