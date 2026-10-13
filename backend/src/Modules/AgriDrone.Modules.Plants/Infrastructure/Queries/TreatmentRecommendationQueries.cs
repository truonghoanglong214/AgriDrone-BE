using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class TreatmentRecommendationQueries(PlantsDbContext context)
    : ITreatmentRecommendationQueries
{
    public async Task<TreatmentRecommendationVersion?> GetApplicableAsync(
        Guid plantConditionId,
        Guid healthLevelId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            plantConditionId,
            Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(healthLevelId, Guid.Empty);
        EnsureUtc(evaluatedAt);

        var matches = await BaseQuery()
            .Where(recommendation =>
                recommendation.PlantConditionId == plantConditionId &&
                recommendation.HealthLevelId == healthLevelId &&
                recommendation.Status == TreatmentRecommendationStatus.Published &&
                recommendation.EffectiveFrom <= evaluatedAt &&
                (!recommendation.EffectiveTo.HasValue ||
                 evaluatedAt < recommendation.EffectiveTo.Value))
            .OrderByDescending(recommendation => recommendation.VersionNumber)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                "More than one treatment recommendation is applicable for the same condition and health level.");
        }

        return matches.SingleOrDefault();
    }

    public async Task<IReadOnlyList<TreatmentRecommendationVersion>>
        GetHistoryAsync(
            string code,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await BaseQuery()
            .Where(recommendation => recommendation.Code == normalizedCode)
            .OrderByDescending(recommendation => recommendation.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<TreatmentRecommendationVersion> BaseQuery() =>
        context.TreatmentRecommendations
            .AsNoTracking()
            .Select(recommendation => new TreatmentRecommendationVersion(
                recommendation.Id,
                recommendation.Code,
                recommendation.VersionNumber,
                recommendation.PlantConditionId,
                recommendation.HealthLevelId,
                recommendation.Title,
                recommendation.Guidance,
                recommendation.AdvisoryDisclaimer,
                recommendation.ExpertSource,
                recommendation.SourceReference,
                recommendation.Status,
                recommendation.EffectiveFrom,
                recommendation.EffectiveTo));

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
