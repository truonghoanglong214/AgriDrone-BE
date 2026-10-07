namespace AgriDrone.Modules.Farms.Domain.Boundaries;

internal interface IFarmBoundaryRepository
{
    Task<FarmBoundary?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid boundaryId,
        CancellationToken cancellationToken = default);

    Task<FarmBoundary?> GetCurrentApprovedAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    void Add(FarmBoundary boundary);
    void Update(FarmBoundary boundary);
}
