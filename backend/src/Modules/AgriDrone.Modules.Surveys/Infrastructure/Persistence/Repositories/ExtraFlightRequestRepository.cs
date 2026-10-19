using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class ExtraFlightRequestRepository(SurveysDbContext context)
    : IExtraFlightRequestRepository
{
    public Task<ExtraFlightRequest?> GetAsync(
        Guid surveyOrderId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        context.ExtraFlightRequests.SingleOrDefaultAsync(
            item => item.SurveyOrderId == surveyOrderId && item.Id == requestId,
            cancellationToken);

    public void Add(ExtraFlightRequest request) =>
        context.ExtraFlightRequests.Add(request);
}
