using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class SurveyOrderArchiveReferenceQuery(
    SurveysDbContext dbContext)
    : ISurveyOrderArchiveReferenceQuery
{
    public Task<int> CountActiveForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.SurveyOrders
            .AsNoTracking()
            .CountAsync(
                order =>
                    order.TenantId == tenantId &&
                    order.FarmId == farmId &&
                    order.Status != SurveyOrderStatus.Completed &&
                    order.Status != SurveyOrderStatus.Cancelled,
                cancellationToken);
}
