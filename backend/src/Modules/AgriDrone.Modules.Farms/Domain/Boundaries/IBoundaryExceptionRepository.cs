namespace AgriDrone.Modules.Farms.Domain.Boundaries;

internal interface IBoundaryExceptionRepository
{
    Task<BoundaryException?> GetByIdAsync(
        Guid tenantId,
        Guid exceptionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasUnresolvedSourceAsync(
        Guid farmBoundaryVersionId,
        BoundaryExceptionSource source,
        string sourceReferenceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoundaryException>> ListByFarmAsync(
        Guid tenantId,
        Guid farmId,
        BoundaryExceptionState? state = null,
        CancellationToken cancellationToken = default);

    void Add(BoundaryException boundaryException);
    void Update(BoundaryException boundaryException);
}
