namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries;

public interface IFarmActivateDependency
{
    Task<ActivateDependencySummary> GetForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<ActivateDependencySummary> GetForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);
}

public sealed record ActivateDependencySummary(
    int ActiveZoneCount,
    int ActiveMissionCount,
    int OpenFieldTaskCount)
{
    public bool HasAny =>
        DeactiveZoneCount > 0 ||
        DeactiveMissionCount > 0;
}