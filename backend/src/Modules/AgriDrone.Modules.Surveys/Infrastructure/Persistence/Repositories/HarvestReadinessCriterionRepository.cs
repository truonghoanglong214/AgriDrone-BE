using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class HarvestReadinessCriterionRepository(
    SurveysDbContext context) : IHarvestReadinessCriterionRepository
{
    public Task<HarvestReadinessCriterion?> GetByIdAsync(
        Guid criterionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(criterionId, Guid.Empty);
        return context.HarvestReadinessCriteria.SingleOrDefaultAsync(
            criterion => criterion.Id == criterionId,
            cancellationToken);
    }

    public async Task<HarvestReadinessCriterion?> GetEffectiveAsync(
        string code,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureUtc(evaluatedAt);
        var normalizedCode = code.Trim().ToUpperInvariant();

        var matches = await context.HarvestReadinessCriteria
            .Where(criterion =>
                criterion.Code == normalizedCode &&
                criterion.Status != HarvestReadinessCriterionStatus.Retired &&
                criterion.EffectiveFrom <= evaluatedAt &&
                (!criterion.EffectiveTo.HasValue ||
                 evaluatedAt < criterion.EffectiveTo.Value))
            .OrderByDescending(criterion => criterion.VersionNumber)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Criterion '{normalizedCode}' has overlapping effective versions at '{evaluatedAt:O}'.");
        }

        return matches.SingleOrDefault();
    }

    public async Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalizedCode = code.Trim().ToUpperInvariant();
        var latest = await context.HarvestReadinessCriteria
            .Where(criterion => criterion.Code == normalizedCode)
            .MaxAsync(
                criterion => (int?)criterion.VersionNumber,
                cancellationToken);
        return latest.GetValueOrDefault() + 1;
    }

    public void Add(HarvestReadinessCriterion criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        context.HarvestReadinessCriteria.Add(criterion);
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                nameof(value));
        }
    }
}
