using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Services;

internal sealed class SurveyRequestIdempotencyResolver(
    ISurveyRequestRepository repository)
    : ISurveyRequestIdempotencyResolver
{
    public async Task<SurveyRequestIdempotencyResolution> ResolveAsync(
        RequestIdempotency idempotency,
        SurveyRequestSubmissionSnapshot submission,
        CancellationToken cancellationToken = default)
    {
        if (idempotency == default)
        {
            throw new ArgumentException(
                "Idempotency identity is required.",
                nameof(idempotency));
        }

        ArgumentNullException.ThrowIfNull(submission);
        var existing = await repository
            .GetByCallerScopeAndIdempotencyKeyAsync(
                idempotency,
                cancellationToken);

        if (existing is null)
        {
            return SurveyRequestIdempotencyResolution.NewSubmission();
        }

        var incomingFingerprint =
            SurveyRequestSubmissionFingerprint.Create(submission);
        var existingFingerprint =
            SurveyRequestSubmissionFingerprint.Create(
                SurveyRequestSubmissionSnapshot.From(existing));

        if (!string.Equals(
                incomingFingerprint.Value,
                existingFingerprint.Value,
                StringComparison.Ordinal))
        {
            return SurveyRequestIdempotencyResolution.PayloadMismatch();
        }

        return SurveyRequestIdempotencyResolution.Replay(
            new SurveyRequestAcknowledgementResponse(
                existing.Id,
                existing.RequestNumber,
                existing.Kind,
                existing.Status,
                existing.CreatedAt));
    }
}
