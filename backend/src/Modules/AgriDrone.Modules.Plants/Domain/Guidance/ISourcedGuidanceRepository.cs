namespace AgriDrone.Modules.Plants.Domain.Guidance;

internal interface ISourcedGuidanceRepository
{
    Task<SourcedGuidance?> GetByIdAsync(
        Guid guidanceId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> HasEffectiveOverlapAsync(
        Guid plantConditionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludingGuidanceId = null,
        CancellationToken cancellationToken = default);

    void Add(SourcedGuidance guidance);

    void Update(SourcedGuidance guidance);
}
