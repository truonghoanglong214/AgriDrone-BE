using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class AdjustmentSettlementRepository(SurveysDbContext context)
    : IAdjustmentSettlementRepository
{
    public Task<AdjustmentSettlement?> GetAsync(
        Guid settlementId,
        CancellationToken cancellationToken = default) =>
        context.AdjustmentSettlements.SingleOrDefaultAsync(
            item => item.Id == settlementId,
            cancellationToken);

    public Task<AdjustmentSettlement?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        context.AdjustmentSettlements.SingleOrDefaultAsync(
            item => item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public void Add(AdjustmentSettlement settlement) =>
        context.AdjustmentSettlements.Add(settlement);
}
