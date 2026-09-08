namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries;

public interface IFarmArchiveDependencyQuery
{
    Task<ArchiveDependencySummary> GetForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);

    Task<ArchiveDependencySummary> GetForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public sealed record ArchiveDependencySummary(
    int ActiveZoneCount,
    int ActiveMissionCount,
    int OpenFieldTaskCount)
{
    public bool HasAny =>
        ActiveZoneCount > 0 ||
        ActiveMissionCount > 0 ||
        OpenFieldTaskCount > 0;
}
