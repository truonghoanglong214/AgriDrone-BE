using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SpatialPolicyRepository(SurveysDbContext context)
    : ISpatialPolicyRepository
{
    public Task<SpatialPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.SpatialPolicies.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<SpatialPolicy?> GetEffectiveAsync(
        string streamKey,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = streamKey?.Trim().ToUpperInvariant();
        return context.SpatialPolicies
            .Where(p => p.StreamKey == normalizedKey &&
                        p.EffectiveFrom <= evaluatedAt &&
                        (p.EffectiveTo == null || p.EffectiveTo > evaluatedAt))
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(SpatialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        context.SpatialPolicies.Add(policy);
    }
}
