using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderCommercialSnapshotQuery(AgriDroneSchemaDbContext db)
    : ISurveyOrderCommercialSnapshotQuery
{
    public Task<SurveyOrderMappingContext?> GetMappingContextAsync(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid captureAttemptId,
        CancellationToken cancellationToken)
    {
        return (
            from order in db.Set<SurveyOrder>().AsNoTracking()
            join commercial in db.Set<SurveyOrderCommercialSnapshot>() on order.Id equals commercial.SurveyOrderId
            join attempt in db.Set<CaptureAttempt>() on order.Id equals attempt.OrderId
            where order.Id == surveyOrderId &&
                  order.TenantId == tenantId && order.FarmId == farmId &&
                  order.Scope == ContractScope.V3 && order.Status == SurveyOrderStatus.Processing &&
                  order.FarmBoundaryVersionId.HasValue && commercial.MapsPoles &&
                  attempt.Id == captureAttemptId && attempt.MissionId == missionId &&
                  attempt.TenantId == tenantId && attempt.FarmId == farmId &&
                  attempt.Status == CaptureAttemptStatus.Processing
            select new SurveyOrderMappingContext(
                order.FarmBoundaryVersionId!.Value,
                order.RequiresBaselineMapping,
                order.FarmBaseMapVersionId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await db.Set<SurveyOrderCommercialSnapshot>()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.SurveyOrderId == surveyOrderId, cancellationToken);

        if (snapshot is null) return null;

        return new PinnedSurveyOrderCommercialSnapshot(
            snapshot.SurveyOrderId,
            snapshot.BusinessPolicyVersionId,
            snapshot.ServiceCode,
            snapshot.MapsPoles,
            snapshot.ScreensDisease,
            snapshot.PriceVersionId,
            snapshot.PricePerPole,
            snapshot.Currency);
    }
}
