using AgriDrone.Modules.Plants.Domain.Recommendations;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class TreatmentRecommendationRepository(PlantsDbContext context)
    : ITreatmentRecommendationRepository
{
    public Task<TreatmentRecommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
        => context.TreatmentRecommendations.SingleOrDefaultAsync(
            recommendation => recommendation.Id == recommendationId,
            cancellationToken);

    public Task<TreatmentRecommendation?> GetCurrentPublishedAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return context.TreatmentRecommendations.SingleOrDefaultAsync(
            recommendation => recommendation.Code == normalizedCode &&
                              recommendation.Status == TreatmentRecommendationStatus.Published,
            cancellationToken);
    }

    public async Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var current = await context.TreatmentRecommendations
            .Where(recommendation => recommendation.Code == normalizedCode)
            .MaxAsync(recommendation => (int?)recommendation.VersionNumber, cancellationToken);
        return (current ?? 0) + 1;
    }

    public void Add(TreatmentRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        context.TreatmentRecommendations.Add(recommendation);
    }

    public void Update(TreatmentRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        context.TreatmentRecommendations.Update(recommendation);
    }
}
