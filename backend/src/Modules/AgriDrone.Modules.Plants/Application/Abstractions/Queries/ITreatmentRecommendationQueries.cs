using AgriDrone.Modules.Plants.Domain.Recommendations;

namespace AgriDrone.Modules.Plants.Application.Abstractions.Queries;

internal interface ITreatmentRecommendationQueries
{
    Task<TreatmentRecommendationVersion?> GetApplicableAsync(
        Guid plantConditionId,
        Guid healthLevelId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TreatmentRecommendationVersion>> GetHistoryAsync(
        string code,
        CancellationToken cancellationToken = default);
}

internal sealed record TreatmentRecommendationVersion(
    Guid RecommendationId,
    string Code,
    int VersionNumber,
    Guid PlantConditionId,
    Guid HealthLevelId,
    string Title,
    string Guidance,
    string AdvisoryDisclaimer,
    string ExpertSource,
    string SourceReference,
    TreatmentRecommendationStatus Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);
