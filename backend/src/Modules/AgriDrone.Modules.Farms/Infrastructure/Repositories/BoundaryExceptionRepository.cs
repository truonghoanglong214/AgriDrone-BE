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

    public async Task<IReadOnlyList<BoundaryException>> ListByFarmAsync(
        Guid tenantId,
        Guid farmId,
        BoundaryExceptionState? state = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.BoundaryExceptions
            .Where(b => b.TenantId == tenantId && b.FarmId == farmId);

        if (state.HasValue)
        {
            query = query.Where(b => b.State == state.Value);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

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
