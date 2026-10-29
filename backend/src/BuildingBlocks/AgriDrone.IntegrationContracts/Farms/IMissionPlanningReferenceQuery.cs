namespace AgriDrone.IntegrationContracts.Farms;

public interface IMissionPlanningReferenceQuery
{
    Task<bool> IsActiveZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);

    Task<bool> IsConfirmedMapVersionAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default);

    Task<bool> IsPublishedFarmBaseMapVersionAsync(
        Guid tenantId,
        Guid farmId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
