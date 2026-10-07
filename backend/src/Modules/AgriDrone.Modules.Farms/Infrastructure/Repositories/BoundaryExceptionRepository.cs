using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Repositories;

internal sealed class BoundaryExceptionRepository(FarmsDbContext context)
    : IBoundaryExceptionRepository
{
    public Task<BoundaryException?> GetByIdAsync(
        Guid tenantId,
        Guid exceptionId,
        CancellationToken cancellationToken = default)
        => context.BoundaryExceptions.SingleOrDefaultAsync(
            boundaryException => boundaryException.TenantId == tenantId &&
                                 boundaryException.Id == exceptionId,
            cancellationToken);

    public Task<bool> HasUnresolvedSourceAsync(
        Guid farmBoundaryVersionId,
        BoundaryExceptionSource source,
        string sourceReferenceId,
        CancellationToken cancellationToken = default)
        => context.BoundaryExceptions.AnyAsync(
            boundaryException => boundaryException.FarmBoundaryVersionId == farmBoundaryVersionId &&
                                 boundaryException.Source == source &&
                                 boundaryException.SourceReferenceId == sourceReferenceId &&
                                 boundaryException.State != BoundaryExceptionState.Resolved,
            cancellationToken);

    public void Add(BoundaryException boundaryException)
    {
        ArgumentNullException.ThrowIfNull(boundaryException);
        context.BoundaryExceptions.Add(boundaryException);
    }

    public void Update(BoundaryException boundaryException)
    {
        ArgumentNullException.ThrowIfNull(boundaryException);
        context.BoundaryExceptions.Update(boundaryException);
    }
}
