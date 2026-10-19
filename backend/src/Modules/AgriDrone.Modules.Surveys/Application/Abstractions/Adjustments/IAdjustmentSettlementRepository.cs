using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;

public interface IAdjustmentSettlementRepository
{
    Task<AdjustmentSettlement?> GetAsync(
        Guid settlementId,
        CancellationToken cancellationToken = default);

    Task<AdjustmentSettlement?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    void Add(AdjustmentSettlement settlement);
}
