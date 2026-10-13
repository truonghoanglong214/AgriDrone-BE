using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalOrderRepository(
    SurveyApprovalDbContext context)
    : ISurveyOrderRepository
{
    public Task<SurveyOrder?> GetBySurveyRequestIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default) =>
        context.SurveyOrders.SingleOrDefaultAsync(
            order => order.SurveyRequestId == surveyRequestId,
            cancellationToken);

    public void Add(SurveyOrder surveyOrder)
    {
        ArgumentNullException.ThrowIfNull(surveyOrder);
        context.EnsureApprovalTransaction();
        context.SurveyOrders.Add(surveyOrder);
    }
}
