namespace AgriDrone.Modules.Surveys.Domain;

internal interface IHarvestReadinessCriterionRepository
{
    Task<HarvestReadinessCriterion?> GetByIdAsync(
        Guid criterionId,
        CancellationToken cancellationToken = default);

    Task<HarvestReadinessCriterion?> GetEffectiveAsync(
        string code,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default);

    void Add(HarvestReadinessCriterion criterion);
}
