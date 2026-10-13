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

    void Add(BoundaryException boundaryException);
    void Update(BoundaryException boundaryException);
}
