namespace AgriDrone.IntegrationContracts.Farms;

public interface IFarmAssignmentReferenceQuery
{
    Task<bool> IsActiveFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FarmAssignmentReference>> GetActiveFarmsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FarmAssignmentZoneReference>> GetActiveZonesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> farmIds,
        CancellationToken cancellationToken = default);
}

public sealed record FarmAssignmentReference(
    Guid FarmId,
    string Code,
    string Name,
    string? Address,
    decimal? AreaHectares);

public sealed record FarmAssignmentZoneReference(
    Guid FarmId,
    Guid ZoneId,
    string Code,
    string Name,
    decimal? AreaHectares);
