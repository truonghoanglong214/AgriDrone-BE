using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyRequestRepository(SurveysDbContext context)
    : ISurveyRequestRepository
{
    public Task<SurveyRequest?> GetByIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyRequestId,
            Guid.Empty);

        return context.SurveyRequests
            .Include(request => request.SurveyService)
            .Include(request => request.Reviews)
            .SingleOrDefaultAsync(
                request => request.Id == surveyRequestId,
                cancellationToken);
    }

    public Task<SurveyRequest?> GetByCallerScopeAndIdempotencyKeyAsync(
        RequestIdempotency idempotency,
        CancellationToken cancellationToken = default)
    {
        if (idempotency == default)
        {
            throw new ArgumentException(
                "Idempotency identity is required.",
                nameof(idempotency));
        }

        return context.SurveyRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request =>
                    request.CallerScope == idempotency.CallerScope &&
                    request.IdempotencyKey == idempotency.Key,
                cancellationToken);
    }

    public void Add(SurveyRequest surveyRequest)
    {
        ArgumentNullException.ThrowIfNull(surveyRequest);
        context.SurveyRequests.Add(surveyRequest);
    }
}
