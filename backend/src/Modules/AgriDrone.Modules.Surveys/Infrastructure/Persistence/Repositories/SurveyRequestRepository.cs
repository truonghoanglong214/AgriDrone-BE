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

    public Task<SurveyRequestQuoteSnapshot?> GetQuoteSnapshotByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, Guid.Empty);

        return context.SurveyRequestQuoteSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                q => q.SurveyRequestId == requestId,
                cancellationToken);
    }

    public Task<SurveyRequestTracking?> GetTrackingByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, Guid.Empty);

        return context.SurveyRequestTrackings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                t => t.SurveyRequestId == requestId,
                cancellationToken);
    }

    public Task<SurveyRequestTracking?> GetTrackingByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return context.SurveyRequestTrackings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                t => t.TrackingTokenHash == tokenHash,
                cancellationToken);
    }

    public void AddQuoteSnapshot(SurveyRequestQuoteSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        context.SurveyRequestQuoteSnapshots.Add(snapshot);
    }

    public void AddTracking(SurveyRequestTracking tracking)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        context.SurveyRequestTrackings.Add(tracking);
    }
}
