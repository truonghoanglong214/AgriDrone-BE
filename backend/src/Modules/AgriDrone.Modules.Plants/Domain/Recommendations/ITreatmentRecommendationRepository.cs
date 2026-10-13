namespace AgriDrone.Modules.Plants.Domain.Recommendations;

internal interface ITreatmentRecommendationRepository
{
    Task<TreatmentRecommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default);

    Task<TreatmentRecommendation?> GetCurrentPublishedAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default);

    void Add(TreatmentRecommendation recommendation);
    void Update(TreatmentRecommendation recommendation);
}
