using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class SourcedGuidanceRepository(PlantsDbContext context)
    : ISourcedGuidanceRepository
{
    public Task<SourcedGuidance?> GetByIdAsync(
        Guid guidanceId,
        CancellationToken cancellationToken = default) =>
        context.SourcedGuidance.SingleOrDefaultAsync(
            item => item.Id == guidanceId,
            cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var latest = await context.SourcedGuidance
            .Where(item => item.Code == normalized)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken);
        return checked((latest ?? 0) + 1);
    }

    public Task<bool> HasEffectiveOverlapAsync(
        Guid plantConditionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludingGuidanceId = null,
        CancellationToken cancellationToken = default) =>
        context.SourcedGuidance.AsNoTracking().AnyAsync(item =>
            item.PlantConditionId == plantConditionId &&
            item.Status == SourcedGuidanceStatus.Active &&
            (!excludingGuidanceId.HasValue || item.Id != excludingGuidanceId) &&
            (!effectiveTo.HasValue || item.EffectiveFrom < effectiveTo.Value) &&
            (!item.EffectiveTo.HasValue || effectiveFrom < item.EffectiveTo.Value),
            cancellationToken);

    public void Add(SourcedGuidance guidance) => context.SourcedGuidance.Add(guidance);

    public void Update(SourcedGuidance guidance) => context.SourcedGuidance.Update(guidance);
}
