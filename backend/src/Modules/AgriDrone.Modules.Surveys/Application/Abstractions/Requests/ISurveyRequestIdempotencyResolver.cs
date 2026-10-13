using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Requests;

internal interface ISurveyRequestIdempotencyResolver
{
    Task<SurveyRequestIdempotencyResolution> ResolveAsync(
        RequestIdempotency idempotency,
        SurveyRequestSubmissionSnapshot submission,
        CancellationToken cancellationToken = default);
}

internal enum SurveyRequestIdempotencyOutcome
{
    NewSubmission = 0,
    Replay = 1,
    PayloadMismatch = 2
}

internal sealed record SurveyRequestIdempotencyResolution(
    SurveyRequestIdempotencyOutcome Outcome,
    SurveyRequestAcknowledgementResponse? ExistingResponse)
{
    public static SurveyRequestIdempotencyResolution NewSubmission() =>
        new(SurveyRequestIdempotencyOutcome.NewSubmission, null);

    public static SurveyRequestIdempotencyResolution Replay(
        SurveyRequestAcknowledgementResponse response) =>
        new(SurveyRequestIdempotencyOutcome.Replay, response);

    public static SurveyRequestIdempotencyResolution PayloadMismatch() =>
        new(SurveyRequestIdempotencyOutcome.PayloadMismatch, null);
}
